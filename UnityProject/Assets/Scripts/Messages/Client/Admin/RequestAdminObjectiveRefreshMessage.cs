﻿using Mirror;
namespace Messages.Client.Admin
{
	public class RequestAdminObjectiveRefreshMessage : ClientMessage<RequestAdminObjectiveRefreshMessage.NetMessage>
	{
		public struct NetMessage : NetworkMessage
		{
			public string playerForRequestID;
		}

		public override void Process(NetMessage msg)
		{
			if (HasPermission(TAG.MANAGE_ANTAGONISTS))
			{
				ObjectiveRefreshMessage.Send(SentByPlayer.GameObject, SentByPlayer.AccountId, msg.playerForRequestID);
			}
		}

		public static NetMessage Send(string playerForRequestID)
		{
			NetMessage msg = new NetMessage
			{
				playerForRequestID = playerForRequestID
			};

			Send(msg);
			return msg;
		}
	}
}