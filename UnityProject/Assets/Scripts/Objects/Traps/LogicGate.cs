using UnityEngine;
using Objects.Traps;
using Logs;
using Mirror;
using UI.Systems.Tooltips.HoverTooltips;
using System.Collections.Generic;

namespace Objects.Logic
{
	public class LogicGate : GenericTriggerOutput, ICheckedInteractable<HandApply>, IHoverTooltip
	{
		[SerializeField] private LogicInput inputA, inputB;
		[SerializeField] private LogicGateType gateType = LogicGateType.AND;
		[SerializeField] private bool negateOutput = false;

		private bool state = false;
		[SyncVar] private int currentInputInteractingWith = 0;

		[SerializeField] private SpriteHandler inputAHandler = null;
		[SerializeField] private SpriteHandler inputBHandler = null;
		[SerializeField] private SpriteHandler outputHandler = null;

		protected override void Awake()
		{
			if(inputA == null || inputB == null)
			{
				Loggy.Error($"[LogicGate/Awake] Error during initialisation, missing logic input");
				return;
			}

			inputA.OnStateChangeEvent += OnInputUpdate;
			inputB.OnStateChangeEvent += OnInputUpdate;
			currentInputInteractingWith = 0;

			SyncList();
			OnInputUpdate(); //Affirm Inital State (Might start on)
		}

		private void OnInputUpdate()
		{
			bool oldState = state;

			switch(gateType)
			{
				case LogicGateType.AND:
					state = inputA.State & inputB.State;
					break;
				case LogicGateType.OR:
					state = inputA.State | inputB.State;
					break;
				case LogicGateType.XOR:
					state = inputA.State ^ inputB.State;
					break;
				default:
					state = inputA.State & inputB.State;
					break;
			}

			inputAHandler.SetSpriteVariant(inputA.State ? 1 : 0);
			inputBHandler.SetSpriteVariant(inputB.State ? 1 : 0);

			if (oldState != state) OnStateChange();
		}

		private void OnStateChange()
		{
			if (state == !negateOutput) TriggerOutput();
			else ReleaseOutput();

			outputHandler.SetSpriteVariant(state == !negateOutput ? 1 : 0);
		}

		public bool TryRetrieveTrigger(out IGenericTrigger trigger)
		{
			trigger = currentInputInteractingWith == 0 ? inputA : inputB;
			return trigger != null;
		}

		public bool WillInteract(HandApply interaction, NetworkSide side)
		{
			if (DefaultWillInteract.Default(interaction, side, AllowTelekinesis: false) == false) return false;

			// only allow interactions targeting this
			if (interaction.TargetObject != gameObject) return false;

			return true;
		}

		//With multiple possible inputs to connect with a multitool, we allow the player to switch between what one they want to connect to.
		public void ServerPerformInteraction(HandApply interaction)
		{
			currentInputInteractingWith = currentInputInteractingWith == 0 ? 1 : 0; //Toggle active input to interact with
			if (TryRetrieveTrigger(out var trigger)) Chat.AddExamineMsgFromServer(interaction.Performer, $"You change the active input on the gate to {trigger.gameObject.name}");
			else Loggy.Error("[LogicGate/ServerPerformInteraction] Attempted to toggle active input but the trigger was NULL!");

		}

		#region Tooltips

		public string HoverTip()
		{
			return null;
		}

		public string CustomTitle()
		{
			return null;
		}

		public Sprite CustomIcon()
		{
			return null;
		}

		public List<Sprite> IconIndicators()
		{
			return null;
		}

		public List<TextColor> InteractionsStrings()
		{
			List<TextColor> interactions = new List<TextColor>();

			if (TryRetrieveTrigger(out var trigger))
			{
				TextColor text = new TextColor
				{
					Text = $"Interact to change active input for multitool interaction.\nCurrently interacting with: {trigger.gameObject.name}",
					Color = IntentColors.Help
				};

				interactions.Add(text);
			}
			else Loggy.Error("[LogicGate/InteractionsString] Attempted to display current input but the trigger was NULL!");


			return interactions;
		}
		#endregion
	}

	public enum LogicGateType
	{
		AND = 0,
		OR = 1,
		XOR = 2,
	}
}
