using System.Collections;
using System.Collections.Generic;
using Messages.Server;
using Mirror;
using Tilemaps.Behaviours.Layers;
using UnityEngine;

public class MetaDataLayerMessage : ServerMessage<MetaDataLayerMessage.NetMessage>
{
	public struct NetMessage : NetworkMessage
	{
		public List<DelayedData> Changes;
		public uint MatrixSyncNetID;
	}

	//just a best guess, try increasing it until the message exceeds mirror's limit
	private static readonly int MAX_CHANGES_PER_MESSAGE = 350;


	public override void Process(NetMessage msg)
	{
		if (CustomNetworkManager.IsServer) return;
		LoadNetworkObject(msg.MatrixSyncNetID);

		var MetaDataLayer = NetworkObject.transform.parent.GetComponentInChildren<MetaDataLayer>();
		foreach (var Change in msg.Changes)
		{
			var node = MetaDataLayer.Get(Change.Position);
			node.IsSlippery = Change.IsSlippy;
			node.IsIceSlippy = Change.IsIceSlippy;
			node.IsSuperSlippery = Change.IsSuperSlippy;
		}
	}

	public static void SendTo(GameObject managerSubject, NetworkConnection recipient,
		Dictionary<Vector3Int, MetaDataNode> changeList)
	{
		if (changeList == null || changeList.Count == 0) return;
		var netID = managerSubject.transform.parent.GetComponent<NetworkedMatrix>().MatrixSync.netId;
		foreach (var changeChunk in changeList.Chunk(MAX_CHANGES_PER_MESSAGE))
		{
			List<DelayedData> Changes = new List<DelayedData>();

			foreach (var metaData in changeChunk)
			{
				Changes.Add(new DelayedData()
				{
					Position = metaData.Key,
					IsSlippy = metaData.Value.IsSlippery,
					IsIceSlippy = metaData.Value.IsIceSlippy,
					IsSuperSlippy = metaData.Value.IsSuperSlippery
				});
			}

			NetMessage msg = new NetMessage
			{
				MatrixSyncNetID = netID,
				Changes = Changes
			};

			SendTo(recipient, msg);
		}
	}


	public static void Send(GameObject managerSubject, List<MetaDataNode> changeList)
	{
		var netID = managerSubject.transform.parent.GetComponent<NetworkedMatrix>().MatrixSync.netId;
		foreach (var changeChunk in changeList.Chunk(MAX_CHANGES_PER_MESSAGE))
		{
			List<DelayedData> Changes = new List<DelayedData>();

			foreach (var metaData in changeChunk)
			{
				Changes.Add(new DelayedData()
				{
					Position = metaData.LocalPosition,
					IsSlippy = metaData.IsSlippery,
					IsIceSlippy = metaData.IsIceSlippy
				});
			}

			NetMessage msg = new NetMessage
			{
				MatrixSyncNetID = netID,
				Changes = Changes
			};
			SendToAll(msg);
		}
	}

	public struct DelayedData
	{
		public Vector3Int Position;
		public bool IsSlippy;
		public bool IsIceSlippy;
		public bool IsSuperSlippy;
	}
}

public static class UpdateTileMessageReaderWriters
{
	public static MetaDataLayerMessage.NetMessage Deserialize(this NetworkReader reader)
	{
		var message = new MetaDataLayerMessage.NetMessage();
		message.Changes = new List<MetaDataLayerMessage.DelayedData>();
		message.MatrixSyncNetID = reader.ReadUInt();
		while (true)
		{
			var Continue = reader.ReadBool();
			if (Continue == false)
			{
				break;
			}

			var WorkingOn = new MetaDataLayerMessage.DelayedData
			{
				Position = reader.ReadVector3Int(),
				IsSlippy = reader.ReadBool(),
				IsIceSlippy = reader.ReadBool()
			};

			message.Changes.Add(WorkingOn);
		}

		return message;
	}

	public static void Serialize(this NetworkWriter writer, MetaDataLayerMessage.NetMessage message)
	{
		writer.WriteUInt(message.MatrixSyncNetID);
		foreach (var delayedData in message.Changes)
		{
			writer.WriteBool(true);

			writer.WriteVector3Int(delayedData.Position);
			writer.WriteBool(delayedData.IsSlippy);
			writer.WriteBool(delayedData.IsIceSlippy);
		}

		writer.WriteBool(false);
	}
}