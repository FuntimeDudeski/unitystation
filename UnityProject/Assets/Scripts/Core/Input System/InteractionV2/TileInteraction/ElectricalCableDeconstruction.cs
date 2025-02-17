﻿using System.Collections;
using System.Collections.Generic;
using HealthV2;
using UnityEngine;
using Systems.Electricity;

namespace Tiles.Electrical
{
	/// <summary>
	/// Used for deconstructing cables in a Thread Friendly way
	/// </summary>
	[CreateAssetMenu(fileName = "ElectricalCableDeconstruction",
		menuName = "Interaction/TileInteraction/ElectricalCableDeconstruction")]
	public class ElectricalCableDeconstruction : TileInteraction
	{
		[Tooltip(
			"Trait required on the used item in order to deconstruct the tile. If welder, will check if welder is on.")]
		[SerializeField]
		private ItemTrait requiredTrait = null;

		// TODO Unused. See https://github.com/unitystation/unitystation/issues/3903
		[Tooltip("Action message to performer when they begin this interaction.")]
		[SerializeField]
		private string performerStartActionMessage = null;

		[Tooltip(
			"Use {performer} for performer name. Action message to others when the performer begins this interaction.")]
		[SerializeField]
		private string othersStartActionMessage = null;

		// Start is called before the first frame update
		public override bool WillInteract(TileApply interaction, NetworkSide side)
		{
			if (!DefaultWillInteract.Default(interaction, side)) return false;
			if (requiredTrait == CommonTraits.Instance.Welder)
			{
				return Validations.HasUsedActiveWelder(interaction);
			}

			return Validations.HasItemTrait(interaction.HandObject, requiredTrait);
		}

		public override void ServerPerformInteraction(TileApply interaction)
		{
			string othersMessage = Chat.ReplacePerformer(othersStartActionMessage, interaction.Performer);
			Chat.AddActionMsgToChat(interaction.Performer, performerStartActionMessage, othersMessage);
			if (interaction.BasicTile.LayerType != LayerType.Electrical) return;

			var electricalCable = interaction.BasicTile as ElectricalCableTile;
			if (electricalCable == null) return;

			var matrix = interaction.TileChangeManager.MetaTileMap.Layers[LayerType.Electrical].Matrix;
			var metaDataNode = matrix.GetMetaDataNode(interaction.TargetCellPos);

			foreach (var electricalData in metaDataNode.ElectricalData)
			{
				if (electricalData.RelatedTile != electricalCable) continue;

				// Electrocute the performer. If shock is painful enough, cancel the interaction.
				ElectricityFunctions.WorkOutActualNumbers(electricalData.InData);
				float voltage = electricalData.InData.Data.ActualVoltage;
				var electrocution = new Electrocution(voltage, interaction.WorldPositionTarget, "cable");
				var performerLhb = interaction.Performer.GetComponent<LivingHealthMasterBase>();
				var severity = performerLhb.Electrocute(electrocution);
				if (severity > LivingShockResponse.Mild) return;

				electricalData.InData.DestroyThisPlease();
				return;
			}
		}
	}
}
