using System;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;
using UI.Systems.Tooltips.HoverTooltips;
using UnityEngine.Events;
using Util.Independent.FluentRichText;


namespace Items.Food
{
	/// <summary>
	/// This class doesn't handle cooking itself; it merely stores the cooking times
	/// and products for other objects (e.g. the microwave) to use when cooking.
	/// <para>The <see cref="OnCooked"/> event is raised when something cooks this, which other
	/// components can subscribe to, to perform extra logic (for e.g. microwaving dice to rig them).</para>
	/// </summary>
	public class Cookable : MonoBehaviour, IExaminable
	{
		[SerializeField]
		private Integrity integrity;

		[Tooltip("Minimum time to cook.")]
		public int CookTime = 10;

		[InfoBox("If no transormation is to take place, then don't select any item. Don't select the same item as itself.", EInfoBoxType.Warning)]
		[Tooltip("What this item becomes when cooked." +
				"If not set, this item will not change GameObject when cooked, but will still invoke the cooked event.")]
		public GameObject CookedProduct;

		[Tooltip("What methods this item can be cooked by.")]
		[EnumFlag]
		public CookSource CookableBy = CookSource.All;

		/// <summary>
		/// Raised when enough cooking time has been added (via <see cref="AddCookingTime(float)"/>)
		/// </summary>
		public UnityEvent OnCooked;

		private float timeSpentCooking;

		private Pickupable Pickupable;

		[BoxGroup("Cooking by damage settings")]
		[field: SerializeField]
		public float minimumDamage = 2f;
		[BoxGroup("Cooking by damage settings")]
		[field: SerializeField]
		public DamageType cookingByDamageType = DamageType.Burn;
		[BoxGroup("Cooking by damage settings")]
		[field: SerializeField]
		public AttackType cookingByAttackType = AttackType.Fire;

		private void Awake()
		{
			if (integrity == null) integrity = GetComponent<Integrity>();
			Pickupable = this.GetComponent<Pickupable>();
			integrity.OnApplyDamage.AddListener(OnDamageReceived);
			if (CookableBy.HasFlag(CookSource.BurnUp))
			{
				integrity.OnBurnUpServer += OnBurnUpServer;
			}
			OnCooked.AddListener(CookProduct);
		}

		private void OnDestroy()
		{
			OnCooked.RemoveAllListeners();
		}

		private void OnDamageReceived(DamageInfo info)
		{
			if (info.Damage < minimumDamage) return;
			if (info.DamageType != cookingByDamageType && info.AttackType != cookingByAttackType) return;
			AddCookingTime(info.Damage / 2f);
		}

		/// <summary>
		///  basically resets all the cooking time
		/// </summary>
		public void ResetTimeCooked()
		{
			timeSpentCooking = 0;
		}

		/// <summary>
		/// Adds the given cooking time to this object. Will return true if the item is now cooked.
		/// </summary>
		/// <param name="time">The amount of time in seconds to add to this object's time spent cooking.</param>
		/// <returns>true if the added time and any previous time spent cooking was enough to exceed the required cooking time.</returns>
		public bool AddCookingTime(float time)
		{
			timeSpentCooking += time;
			if (timeSpentCooking > CookTime )
			{
				OnCooked?.Invoke();
				return true;
			}
			return false;
		}

		private void OnBurnUpServer(DestructionInfo info)
		{
			if (CookedProduct == null) return;
			var item = Spawn.ServerPrefab(CookedProduct, gameObject.AssumedWorldPosServer(), transform.parent);

			if (Pickupable.ItemSlot != null)
			{
				Inventory.ServerAdd(item.GameObject, Pickupable.ItemSlot, ReplacementStrategy.DropOther);
			}




		}

		public void CookProduct()
		{
			OnBurnUpServer(null);
			var stackable = this.GetComponent<Stackable>();
			if (stackable != null && stackable.Amount > 1)
			{
				stackable.ServerConsume(1);
				this.ResetTimeCooked();
			}
			else
			{
				_ = Despawn.ServerSingle(this.gameObject);
			}
		}

		public string Examine(Vector3 worldPos = default(Vector3))
		{
			var exanimeInfo = $"This item can be cooked under heat. {CookTime} seconds.";
			if (timeSpentCooking > 0.1f)
			{
				var percentage = (int)((timeSpentCooking / CookTime) * 100);
				exanimeInfo += " [";
				exanimeInfo += $"{percentage}%".Color(Color.green);
				exanimeInfo += "]";
			}
			return exanimeInfo;
		}
	}

	[Flags]
	public enum CookSource
	{
		BurnUp = 0,
		Microwave = 1 << 1,
		Griddle = 1 << 2,
		Oven = 1 << 3,
		All = ~0,
	}
}