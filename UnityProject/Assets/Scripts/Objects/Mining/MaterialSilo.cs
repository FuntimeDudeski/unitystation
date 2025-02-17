﻿using System;
using System.Collections;
using System.Collections.Generic;
using Logs;
using SecureStuff;
using UnityEngine;
using UnityEngine.Serialization;

namespace Objects.Machines
{
	public class MaterialSilo : MonoBehaviour, ICheckedInteractable<HandApply>, IServerLifecycle
	{
		[FormerlySerializedAs("linkedStorages")]
		public List<MaterialStorageLink> InitiallinkedStorages = new List<MaterialStorageLink>();
		private List<MaterialStorageLink> linkedStorages = new List<MaterialStorageLink>();

		private ItemTrait InsertedMaterialType;
		public MaterialStorage materialStorage;

		private bool MapSpawned = false;

		private void Awake()
		{
			linkedStorages.AddRange(InitiallinkedStorages);
			materialStorage = GetComponent<MaterialStorage>();
			CraftingManager.RoundstartStationSilo = this;
		}

		public void Start()
		{
			if (CustomNetworkManager.IsServer == false) return;
			if (MapSpawned == false) return;


			var registerTile = GetComponent<RegisterTile>();
			var array = registerTile.Matrix.GetComponentsInChildren<MaterialStorageLink>();
			foreach (var otherStorage in array)
			{
				var registerObject = otherStorage.GetComponent<RegisterObject>();
				if (registerObject.Matrix.IsMainStation)
				{
					otherStorage.ConnectToSilo(materialStorage);
					linkedStorages.Add(otherStorage);
				}
			}
		}

		public void OnSpawnServer(SpawnInfo info)
		{
			MapSpawned = info.JsonMapped || info.Mapspawn;

		}

		public bool WillInteract(HandApply interaction, NetworkSide side)
		{
			if (!DefaultWillInteract.Default(interaction, side))
				return false;

			InsertedMaterialType = materialStorage.FindMaterial(interaction.HandObject);
			if (InsertedMaterialType != null)
			{
				return true;
			}
			return false;
		}

		public void ServerPerformInteraction(HandApply interaction)
		{
			var stackable = interaction.HandObject.GetComponent<Stackable>();
			materialStorage.TryAddSheet(InsertedMaterialType, stackable.Amount);
			_ = Inventory.ServerDespawn(interaction.HandObject);
		}

		public void OnDespawnServer(DespawnInfo info)
		{
			materialStorage.DropAllMaterials();
			foreach (var linkedMat in linkedStorages)
			{
				if (linkedMat == null) //they were destroyed at some point, irrelevant to us
				{
					continue;
				}
				linkedMat.DisconnectFromSilo();
			}
		}
	}
}