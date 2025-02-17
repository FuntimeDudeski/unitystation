﻿using Core.Admin.Logs;
using Mirror;
using DiscordWebhook;


namespace Messages.Client.Admin
{
	public class RequestLavaLandToggle : ClientMessage<RequestLavaLandToggle.NetMessage>
	{
		public struct NetMessage : NetworkMessage
		{
			public bool LavaLandAllowed;
		}

		public override void Process(NetMessage netMsg)
		{
			if (HasPermission(TAG.ROUND_LAVALAND) == false) return;

			if (SubSceneManager.AdminAllowLavaland == netMsg.LavaLandAllowed) return;

			SubSceneManager.AdminAllowLavaland = netMsg.LavaLandAllowed;

			var state = netMsg.LavaLandAllowed ? "ON" : "OFF";
			var msg = $"Admin: {SentByPlayer.Username}, turned Lava Land spawning {state}";
			AdminLogsManager.AddNewLog(SentByPlayer.GameObject, msg, LogCategory.Admin);
			DiscordWebhookMessage.Instance.AddWebHookMessageToQueue(DiscordWebhookURLs.DiscordWebhookAdminLogURL, msg, "");
		}

		public static NetMessage Send(bool lavaLandAllowed = true)
		{
			NetMessage msg = new NetMessage
			{
				LavaLandAllowed = lavaLandAllowed
			};

			Send(msg);
			return msg;
		}
	}
}
