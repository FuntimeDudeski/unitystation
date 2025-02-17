﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core;
using Player.Movement;
using UnityEngine;
using UniversalObjectPhysics = Core.Physics.UniversalObjectPhysics;

namespace Objects
{
	/// <summary>
	/// Allows objects like chairs, stools to be picked up (by tranforming into an item)
	/// when the object is dragged onto the player's character.
	/// </summary>
	[RequireComponent(typeof(MouseDraggable))]
	public class ObjectPickupable : MonoBehaviour, ICheckedInteractable<MouseDrop>
	{
		[SerializeField] private GameObject prefabVariant = null;
		public bool WillInteract(MouseDrop interaction, NetworkSide side)
		{
			if (!DefaultWillInteract.Default(interaction, side))
			{
				return false;
			}

			if (MatrixManager.GetAt<MovementSynchronisation>(interaction.TargetObject, side)
				.Any(pm => pm.IsBuckled))
			{
				return false;
			}
			var uop = GetComponent<UniversalObjectPhysics>();
			var ps = interaction.Performer.GetComponent<PlayerScript>();
			var pna = interaction.Performer.GetComponent<PlayerNetworkActions>();

			return pna && interaction.Performer == interaction.TargetObject
					   && interaction.DroppedObject == gameObject
					   && pna.GetActiveHandItem() == null
					   && ps.IsRegisterTileReachable(uop.registerTile, side == NetworkSide.Server);
		}

		public void ServerPerformInteraction(MouseDrop interaction)
		{
			var folded = Spawn.ServerPrefab(prefabVariant).GameObject;
			Inventory.ServerAdd(folded,
				interaction.Performer.GetComponent<DynamicItemStorage>().GetActiveHandSlot());
			// Remove from world
			_ = Despawn.ServerSingle(gameObject);
		}
	}
}
