﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Logs;
using Messages.Client;
using Messages.Client.Admin;
using Messages.Server;
using Mirror;
using Newtonsoft.Json;
using Shared.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace AdminTools
{
	public class KickBanEntryPage : SingletonManager<KickBanEntryPage>
	{
		[SerializeField] private GameObject kickPage = null;
		[SerializeField] private GameObject banPage = null;
		[SerializeField] private GameObject jobBanPage = null;

		[SerializeField] private GameObject jobBanJobTemplate = null;

		[SerializeField] private Text kickTitle = null;
		[SerializeField] private InputField kickReasonField = null;

		[SerializeField] private Text banTitle = null;
		[SerializeField] private InputField banReasonField = null;
		[SerializeField] private InputField minutesField = null;

		[SerializeField] private Toggle kickAnnounceToggle = null;
		[SerializeField] private Toggle banAnnounceToggle = null;

		[SerializeField] private Text jobBanTitle = null;
		[SerializeField] private InputField jobBanReasonField = null;
		[SerializeField] private InputField jobBanMinutesField = null;
		[SerializeField] private Toggle jobBanPermaBanToggle = null;
		[SerializeField] private Dropdown jobBanActionAfterDropDown = null;

		private List<JobBanListItem> jobBanJobTypeListObjects = new List<JobBanListItem>();

		private AdminPlayerEntryData playerToKickCache;

		public void SetPage(bool isBan, AdminPlayerEntryData playerToKick, bool isJobBan)
		{
			playerToKickCache = playerToKick;
			UIManager.IsInputFocus = true;
			UIManager.PreventChatInput = true;
			if (!isBan && !isJobBan)
			{
				kickPage.SetActive(true);
				kickTitle.text = $"Kick Player: {playerToKick.name}";
				kickReasonField.text = "";
				kickReasonField.ActivateInputField();
			}
			else if (!isJobBan)
			{
				banPage.SetActive(true);
				banTitle.text = $"Ban Player: {playerToKick.name}";
				banReasonField.text = "";
				banReasonField.ActivateInputField();
				minutesField.text = "";
			}
			else
			{
				jobBanPage.SetActive(true);
				jobBanTitle.text = $"Job Ban Player: {playerToKick.name}";
				jobBanReasonField.text = "";
				jobBanReasonField.ActivateInputField();
				jobBanMinutesField.text = "";
				jobBanPermaBanToggle.isOn = false;

				ClientJobBanDataAdminMessage.Send(playerToKick.uid);

				jobBanActionAfterDropDown.value = 0;
			}

			gameObject.SetActive(true);
		}

		public override void Start()
		{
			base.Start();
			//generate job list

			var jobs = Enum.GetNames(typeof(JobType)).ToList();

			foreach (var jobType in jobs)
			{
				if (jobType == "NULL") continue;

				GameObject jobEntry = Instantiate(jobBanJobTemplate); //creates new button
				jobEntry.SetActive(true);
				var c = jobEntry.GetComponent<JobBanListItem>();
				c.jobName.text = jobType;
				jobBanJobTypeListObjects.Add(c);

				jobEntry.transform.SetParent(jobBanJobTemplate.transform.parent, false);
			}
		}

		public void OnDoKick()
		{
			if (string.IsNullOrEmpty(kickReasonField.text))
			{
				Loggy.Error("Kick reason field needs to be completed!", Category.Admin);
				return;
			}

			RequestKickMessage.Send(playerToKickCache.uid, kickReasonField.text, announce: kickAnnounceToggle.isOn);

			ClosePage();
		}

		public void OnDoBan()
		{
			if (string.IsNullOrEmpty(banReasonField.text))
			{
				Loggy.Error("Ban reason field needs to be completed!", Category.Admin);
				return;
			}

			if (string.IsNullOrEmpty(minutesField.text))
			{
				Loggy.Error("Duration field needs to be completed!", Category.Admin);
				return;
			}

			int.TryParse(minutesField.text, out var minutes);
			RequestBanMessage.Send(playerToKickCache.uid, banReasonField.text, banAnnounceToggle.isOn, minutes);
			ClosePage();
		}

		public void OnDoJobBan()
		{
			if (string.IsNullOrEmpty(jobBanReasonField.text))
			{
				Loggy.Error("Job Ban reason field needs to be completed!", Category.Admin);
				return;
			}

			if (string.IsNullOrEmpty(jobBanMinutesField.text) && jobBanPermaBanToggle.isOn == false)
			{
				Loggy.Error("Duration field needs to be completed or Perma toggled!", Category.Admin);
				return;
			}

			var value = jobBanActionAfterDropDown.value;

			var ghost = value == 2;
			var kick = value == 3;

			int minutes;
			var outSuccess = int.TryParse(jobBanMinutesField.text, out minutes);

			if (!outSuccess && jobBanPermaBanToggle.isOn == false)
			{
				Loggy.Error("Minutes Field incorrectly configured", Category.Admin);
				return;
			}

			if (jobBanPermaBanToggle.isOn == true)
			{
				minutes = 0;
			}

			foreach (var jobs in jobBanJobTypeListObjects)
			{
				if (jobs.toBeBanned.isOn == false) continue;

				var jobTypeBool = Enum.TryParse(jobs.jobName.text, out JobType jobType);

				if (!jobTypeBool) continue;

				PlayerList.RequestJobBan.Send(
					playerToKickCache.uid, jobBanReasonField.text, jobBanPermaBanToggle.isOn, minutes, jobType, ghost,
					kick);
			}

			ClosePage();
		}

		public void ClosePage()
		{
			gameObject.SetActive(false);
			kickPage.SetActive(false);
			banPage.SetActive(false);
			jobBanPage.SetActive(false);
			UIManager.IsInputFocus = false;
			var manager = FindObjectOfType<PlayerManagePage>();
			manager.RefreshPage();
			UIManager.PreventChatInput = false;
		}

		public class ClientJobBanDataAdminMessage : ClientMessage<ClientJobBanDataAdminMessage.NetMessage>
		{
			public struct NetMessage : NetworkMessage
			{
				public string PlayerID;
			}

			public override void Process(NetMessage msg)
			{
				if (HasPermission(TAG.PLAYER_GET_JOB_BANS) == false) return;

				//Server Stuff here

				var jobBanEntries = PlayerList.Instance.ListOfBanEntries(msg.PlayerID);

				ServerSendsJobBanDataAdminMessage.Send(SentByPlayer.Connection, jobBanEntries);
			}

			public static NetMessage Send(string playerID)
			{
				NetMessage msg = new NetMessage
				{
					PlayerID = playerID
				};

				Send(msg);
				return msg;
			}
		}

		public class ServerSendsJobBanDataAdminMessage : ServerMessage<ServerSendsJobBanDataAdminMessage.NetMessage>
		{
			public struct NetMessage : NetworkMessage
			{
				public string JobBanEntries;
			}

			public override void Process(NetMessage msg)
			{
				//client Stuff here
				var bans = JsonConvert.DeserializeObject<List<JobBanEntry>>(msg.JobBanEntries);

				foreach (var jobObject in Instance.jobBanJobTypeListObjects)
				{
					jobObject.toBeBanned.isOn = false;

					if (bans == null || bans.Count == 0)
					{
						jobObject.unbannedStatus.SetActive(true);
						jobObject.bannedStatus.SetActive(false);
						continue;
					}

					foreach (var jobsBanned in bans)
					{
						if (jobObject.jobName.text == jobsBanned.job.ToString())
						{
							jobObject.bannedStatus.SetActive(true);

							var banMsg = "";

							if (jobsBanned.isPerma)
							{
								banMsg = "Perma Banned";
							}
							else
							{
								var entryTime = DateTime.ParseExact(jobsBanned.dateTimeOfBan, "O",
									CultureInfo.InvariantCulture);
								var totalMins = Mathf.Abs((float) (entryTime - DateTime.Now).TotalMinutes);

								banMsg = $"{Mathf.RoundToInt((float) jobsBanned.minutes - totalMins)} minutes left";
							}

							jobObject.banTime.text = banMsg;
							jobObject.unbannedStatus.SetActive(false);
							break;
						}

						jobObject.unbannedStatus.SetActive(true);
						jobObject.bannedStatus.SetActive(false);
					}
				}
			}

			public static NetMessage Send(NetworkConnection requestee, List<JobBanEntry> jobBanEntries)
			{
				NetMessage msg = new NetMessage
				{
					JobBanEntries = JsonConvert.SerializeObject(jobBanEntries)
				};

				SendTo(requestee, msg);
				return msg;
			}
		}
	}
}