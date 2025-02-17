using System.Collections;
using AddressableReferences;
using Chemistry;
using Chemistry.Components;
using Core;
using Mirror;
using Objects;
using UnityEngine;
using Items.Weapons;
using UniversalObjectPhysics = Core.Physics.UniversalObjectPhysics;

namespace Items.Weapons
{
	public class ChemicalGrenade : NetworkBehaviour, IPredictedCheckedInteractable<HandActivate>, IServerDespawn, ITrapComponent,
		ICheckedInteractable<InventoryApply>
	{
		protected ItemStorage containerStorage;
		protected Pickupable pickupable;
		protected UniversalObjectPhysics objectPhysics;

		[SerializeField] protected SpriteHandler spriteHandler;

		protected const int UNLOCKED_SPRITE = 0;
		protected const int LOCKED_SPRITE = 1;
		protected const int ARMED_SPRITE = 2;

		protected const int EMPTY_VARIANT = 0;

		public ReagentContainer ReagentContainer1 =>
			containerStorage.GetIndexedItemSlot(0)?.Item.OrNull()?.GetComponent<ReagentContainer>();

		public ReagentContainer ReagentContainer2 =>
			containerStorage.GetIndexedItemSlot(1)?.Item.OrNull()?.GetComponent<ReagentContainer>();


		protected ReagentContainer mixedReagentContainer;

		[SerializeField] protected bool InitiallyScrewed = false;

		[field: SyncVar] public bool ScrewedClosed { get; protected set; } = false;

		public bool IsFullContainers
		{
			get
			{
				return ReagentContainer1 != null && ReagentContainer2 != null;
			}
		}

		[Header("Explosive properties:"), Space(10)]

		[SerializeField, Tooltip("If the fuse is precise or has a degree of error equal to fuselength / 4")]
		private bool unstableFuse = false;

		[SerializeField, Tooltip("Fuse timer in seconds")]
		private float fuseLength = 3;

		[SerializeField] private AddressableAudioSource armbomb = null;
		[SerializeField] protected float TemperatureChange = 0f;

		private bool hasExploded = false;

		private bool timerRunning = false;

		private const int DEBUG_FOAM_AMOUNT = 50;

		[RightClickMethod()]
		public void DoSmartFoam()
		{
			SmokeAndFoamManager.StartFoamAt(objectPhysics.OfficialPosition, new ReagentMix(), DEBUG_FOAM_AMOUNT, true, true);
		}

		[RightClickMethod()]
		public void DoFoam()
		{
			SmokeAndFoamManager.StartFoamAt(objectPhysics.OfficialPosition, new ReagentMix(), DEBUG_FOAM_AMOUNT, true, false);
		}

		private void Start()
		{
			objectPhysics = GetComponent<UniversalObjectPhysics>();
			mixedReagentContainer = GetComponent<ReagentContainer>();
			containerStorage = GetComponent<ItemStorage>();
			pickupable = GetComponent<Pickupable>();

			ScrewedClosed = InitiallyScrewed;

			// Set grenade to unlocked state by default
			if (ScrewedClosed) UpdateSprite(LOCKED_SPRITE);
			else UpdateSprite(UNLOCKED_SPRITE);
		}

		public void OnDespawnServer(DespawnInfo info)
		{
			// Set grenade to unlocked state by default
			UpdateSprite(UNLOCKED_SPRITE);
			// Reset grenade timer
			timerRunning = false;
			UpdateTimer(timerRunning);
			hasExploded = false;
		}

		public bool WillInteract(HandActivate interaction, NetworkSide side)
		{

			if (DefaultWillInteract.Default(interaction, side) == false) return false;
			if (ScrewedClosed == false)
			{
				Chat.AddExamineMsg(interaction.Performer, "You need to screw together the grenade first numpty");
				return false;
			}

			return true;
		}

		public void ClientPredictInteraction(HandActivate interaction)
		{
			// Toggle the throw action after activation
			UIManager.Action.Throw();
		}

		public void ServerRollbackClient(HandActivate interaction)
		{
		}

		public void ServerPerformInteraction(HandActivate interaction)
		{
			if (timerRunning)
				return;

			// Toggle the throw action after activation
			if (interaction.Performer == PlayerManager.LocalPlayerObject)
			{
				UIManager.Action.Throw();
			}

			// Start timer
			StartCoroutine(TimeExplode(interaction.Performer));
		}

		private IEnumerator TimeExplode(GameObject originator)
		{
			if (ReagentContainer1.ReagentMixTotal == 0 && ReagentContainer2.ReagentMixTotal == 0)
			{
				Chat.AddExamineMsg(originator, "You try to activate the grenade, but the mechanism prevents you from doing so. " +
				                               "It seems like both reagent containers are empty.");
				yield break;
			}
			if (!timerRunning)
			{
				timerRunning = true;
				UpdateTimer(timerRunning);
				_ = SoundManager.PlayNetworkedAtPosAsync(armbomb, originator.AssumedWorldPosServer());

				if (unstableFuse)
				{
					float fuseVariation = fuseLength / 4;
					fuseLength = Random.Range(fuseLength - fuseVariation, fuseLength + fuseVariation);
				}

				yield return WaitFor.Seconds(fuseLength);

				// Is timer still running?
				if (timerRunning)
				{
					timerRunning = false;
					MixReagents();
				}
			}
		}

		/// <summary>
		/// This coroutines make sure that sprite in hands is animated
		/// TODO: replace this with more general aproach for animated icons
		/// </summary>
		/// <returns></returns>
		private IEnumerator AnimateSpriteInHands()
		{
			while (timerRunning && !hasExploded)
			{
				pickupable.RefreshUISlotImage();
				yield return null;
			}
		}

		protected const int DETONATE_SPILL_AMOUNT = 1000; //How much reagent to spill when detonating, designed to empty grenade contents if reactions did not do so.

		public virtual void MixReagents()
		{
			if (isServer)
			{
				var worldPos = objectPhysics.registerTile.WorldPosition;

				BlastData blastData = new BlastData();
				float internalEnergy = ReagentContainer1.CurrentReagentMix.InternalEnergy + ReagentContainer2.CurrentReagentMix.InternalEnergy;

				ReagentContainer1.TransferTo(ReagentContainer1.ReagentMixTotal, mixedReagentContainer, false); //We use false to ensure the reagents do not react before we can obtain our blast data
				ReagentContainer2.TransferTo(ReagentContainer2.ReagentMixTotal, mixedReagentContainer, false);
				ReagentContainer1.ReagentsChanged(true);
				ReagentContainer1.OnReagentMixChanged?.Invoke();
				ReagentContainer2.ReagentsChanged(true);
				ReagentContainer2.OnReagentMixChanged?.Invoke();

				mixedReagentContainer.CurrentReagentMix.InternalEnergy = internalEnergy;
				mixedReagentContainer.CurrentReagentMix.Temperature += TemperatureChange;
				mixedReagentContainer.ReagentsChanged(false, true); //We mix the the two containers, but cache the effects of the mixed container.

				blastData.ReagentMix = mixedReagentContainer.CurrentReagentMix.CloneWithCache();
				ExplosiveBase.ExplosionEvent.Invoke(worldPos, blastData);

				mixedReagentContainer.CurrentReagentMix.ApplyEffectCache(mixedReagentContainer);//We now apply the cache
				mixedReagentContainer.OnReagentMixChanged?.Invoke();
				spriteHandler.SetCatalogueIndexSprite(LOCKED_SPRITE);
				spriteHandler.SetSpriteVariant(EMPTY_VARIANT);
				mixedReagentContainer.Spill(objectPhysics.OfficialPosition.CutToInt(), DETONATE_SPILL_AMOUNT);
			}
		}

		private void UpdateTimer(bool timerRunning)
		{
			this.timerRunning = timerRunning;

			if (timerRunning)
			{
				// Start playing arm animation
				UpdateSprite(ARMED_SPRITE);
				// Update grenade icon in hands
				StartCoroutine(AnimateSpriteInHands());
			}
			else
			{
				// We somehow deactivated bomb
				UpdateSprite(LOCKED_SPRITE);
			}
		}

		public virtual bool WillInteract(InventoryApply interaction, NetworkSide side)
		{
			if (DefaultWillInteract.Default(interaction, side) == false) return false;
			if (interaction.TargetSlot.Item.OrNull()?.gameObject != gameObject) return false;
			if (IsFullContainers && interaction.UsedObject != null)
			{
				if (Validations.HasItemTrait(interaction, CommonTraits.Instance.Screwdriver) == false) return false;
				return true;
			}
			else if (ScrewedClosed == false)
			{
				if (interaction.UsedObject != null)
				{
					if (Validations.HasComponent<ReagentContainer>(interaction.UsedObject) == false) return false;
				}

				return true;
			}

			return false;
		}

		public virtual void ServerPerformInteraction(InventoryApply interaction)
		{
			if (IsFullContainers && interaction.UsedObject != null)
			{
				ScrewedClosed = !ScrewedClosed;

				if (ScrewedClosed) UpdateSprite(LOCKED_SPRITE);
				else UpdateSprite(UNLOCKED_SPRITE);


				var StateText = ScrewedClosed ? "Closed" : "Open";
				Chat.AddActionMsgToChat(interaction, $" you screw the {gameObject.ExpensiveName()} {StateText}",
					$" {interaction.Performer.ExpensiveName()} screws the {gameObject.ExpensiveName()} {StateText}");

				return;
			}

			if (ScrewedClosed == true) return;

			if (interaction.UsedObject != null)
			{
				ItemSlot targetSlot = null;

				if (containerStorage.GetIndexedItemSlot(0).Item == null) targetSlot = containerStorage.GetIndexedItemSlot(0);
				else if (containerStorage.GetIndexedItemSlot(1).Item == null) targetSlot = containerStorage.GetIndexedItemSlot(1);

				if (targetSlot != null) Inventory.ServerTransfer(interaction.FromSlot, targetSlot);
			}
			else
			{
				ItemSlot fromSlot = null;

				if (containerStorage.GetIndexedItemSlot(1).Item != null) fromSlot = containerStorage.GetIndexedItemSlot(1);
				else if (containerStorage.GetIndexedItemSlot(0).Item != null) fromSlot = containerStorage.GetIndexedItemSlot(0);

				if (fromSlot != null) Inventory.ServerTransfer(fromSlot, interaction.FromSlot);
			}

			UpdateSprite(UNLOCKED_SPRITE);
		}

		protected void UpdateSprite(int index)
		{
			spriteHandler?.SetCatalogueIndexSprite(index);

			switch (index)
			{
				case ARMED_SPRITE:
					spriteHandler?.SetSpriteVariant(EMPTY_VARIANT);
					break;

				case LOCKED_SPRITE:
					spriteHandler?.SetSpriteVariant(EMPTY_VARIANT);
					break;

				case UNLOCKED_SPRITE:
					var containers = 0;
					if (containerStorage.GetIndexedItemSlot(0).Item != null) containers++;
					if (containerStorage.GetIndexedItemSlot(1).Item != null) containers++;

					spriteHandler?.SetSpriteVariant(containers);

					break;
			}
		}

		[ContextMenu("Pull a pin")]
		private void PullPin()
		{
			if (ScrewedClosed == false) return;
			UpdateSprite(ARMED_SPRITE);
			StartCoroutine(TimeExplode(gameObject));
		}

		public void TriggerTrap()
		{
			PullPin();
		}
	}
}