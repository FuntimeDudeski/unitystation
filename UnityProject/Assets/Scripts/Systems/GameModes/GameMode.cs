using System;
using System.Collections.Generic;
using System.Linq;
using Antagonists;
using Core.Admin.Logs;
using DiscordWebhook;
using JetBrains.Annotations;
using Logs;
using UI.CharacterCreator;
using UnityEngine;
using UnityEngine.Serialization;
using NaughtyAttributes;
using Managers;
using Mirror;
using StationObjectives;
using Player;

namespace GameModes
{
	/// <summary>
	/// Contains the definition of a game mode. To create a new one you should
	/// make a new class which inherits this one. Make a prefab with this script
	/// attached so you can define the values in the editor and define your
	/// game mode functions in the child class.
	/// </summary>
	public abstract class GameMode : ScriptableObject
	{
		#region Inspector Values

		[Header("General Settings")]
		[Tooltip("The name of the game mode")]
		[SerializeField]
		private string gameModeName = "New Game Mode";
		/// <summary>
		/// The name of the game mode
		/// </summary>
		public string Name => gameModeName;

		[Tooltip("A description of the game mode")]
		[SerializeField]
		[TextArea]
		private string description = "";
		/// <summary>
		/// The description of the game mode
		/// </summary>
		public string Description => description;

		[Tooltip("Should players be allowed to respawn?")]
		[SerializeField]
		private bool canRespawn = false;
		/// <summary>
		/// Is respawning enabled in this game mode
		/// </summary>
		public bool CanRespawn => canRespawn;

		[Tooltip("What is the minimum amount of players needed to play this game mode?")]
		[SerializeField]
		[Min(1)]
		private int minPlayers = 1;
		/// <summary>
		/// The minimum amount of players needed for the game mode to be possible. Can't be lower than 1.
		/// </summary>
		public int MinPlayers => minPlayers;

		[Header("Antagonist Settings")]
		[Tooltip("Ratio of antagonists to player count. A value of 0.2 means there would be " +
		         "2 antagonists when there are 10 players.")]
		[SerializeField]
		[Range(0, 1)]
		private float antagRatio = 0;
		/// <summary>
		/// The ratio of antagonists to spawn for this game mode
		/// </summary>
		public float AntagRatio => antagRatio;

		[Tooltip("The minimum amount of antags needed for the game mode to be possible. " +
		         "If forceMinAntags is true, the number of chosen antags will be rounded up to this number.")]
		[SerializeField]
		[Min(0)]
		private int minAntags = 0;
		/// <summary>
		/// The minimum amount of antags needed for the game mode to be possible.
		/// If <see cref="forceMinAntags"/> is true, the number of chosen antags will be rounded up to this number.
		/// </summary>
		public int MinAntags => minAntags;

		[Tooltip("The maximum amount of antags spawned in the gamemode.")]
		[SerializeField]
		[Min(1)]
		private int maxAntags = 100;

		[SerializeField]
		protected int hardNumberOfAntagsToSpawn = 0;

		/// <summary>
		/// The maximum amount of antags spawned in the gamemode.
		/// If <see cref="forceMinAntags"/> is true, the number of chosen antags will be rounded up to this number.
		/// </summary>
		public int MaxAntags => maxAntags;

		[FormerlySerializedAs("requiresMinAntags")]
		[Tooltip("Is the game mode possible if the player count multiplied by the antagRatio doesn't meet the minAntags? " +
		         "E.g. If true, when antagRatio is 0.2 and minAntags is 1, you need at least 5 players to start the game mode." +
		         "If false then it will force minAntags so 1, and then every 5 players after that if antagRatio is 0.2")]
		[SerializeField]
		private bool forceMinAntags = false;
		/// <summary>
		/// Is the game mode possible if the <see cref="antagRatio"/> doesn't meet the <see cref="minAntags"/>?
		/// E.g. If true, when antagRatio is 0.2 and minAntags is 1, then you need at least 5 players to start the game mode.
		/// If false then it will force minAntags so 1, and then every 5 players after that if antagRatio is 0.2
		/// </summary>
		public bool ForceMinAntags => forceMinAntags;

		[Tooltip("Are antags on the same team or are they lone wolves?" +
		         "Used for the end of round antag report.")]
		[SerializeField]
		private bool teamGameMode = false;
		/// <summary>
		/// Are antags on the same team or are they lone wolves?
		/// Used for the end of round antag report.
		/// </summary>
		public bool TeamGameMode => teamGameMode;

		[Tooltip("Can antags spawn during the round?")]
		[SerializeField]
		private bool midRoundAntags = false;
		/// <summary>
		/// Can antags spawn during the round?
		/// </summary>
		public bool MidRoundAntags => midRoundAntags;

		[Tooltip("The chance for for every possible mid round antag to spawn after start" +
		         "E.G If the gamemode needs another antag say we need two but only have one currently, " +
		         "then every time a player joins they will roll this chance for it. " +
		         "It is multiplied by the amount of open antag slots." +
		         "This stops the players from guessing by player numbers when they should join to get the antag")]
		[SerializeField]
		private int midRoundAntagsChance = 25;

		[Tooltip("The possible antagonists for this game mode")]
		[SerializeField]
		private List<Antagonist> possibleAntags = null;
		/// <summary>
		/// The possible antagonists for this game mode
		/// </summary>
		public List<Antagonist> PossibleAntags => possibleAntags;

		[FormerlySerializedAs("chooseAntagsBeforeJobs")]
		[Tooltip("Should antags be allocated a job? If true, will choose antags after allocating jobs.")]
		[SerializeField, BoxGroup("Job Allocation")]
		private bool allocateJobsToAntags = false;
		/// <summary>
		/// Should antags be allocated a job?
		/// If true, will choose antags after allocating jobs.
		/// </summary>
		public bool AllocateJobsToAntags => allocateJobsToAntags;

		[Tooltip("The JobTypes that cannot be chosen as antagonists for this game mode")]
		[SerializeField, BoxGroup("Job Allocation"), ShowIf(nameof(AllocateJobsToAntags))]
		private List<JobType> nonAntagJobTypes = new List<JobType>
		{
			JobType.CAPTAIN,
			JobType.HOP,
			JobType.HOS,
			JobType.WARDEN,
			JobType.SECURITY_OFFICER,
			JobType.DETECTIVE,
			JobType.AI
		};
		/// <summary>
		/// The JobTypes that cannot be chosen as antagonists for this game mode
		/// </summary>
		public List<JobType> NonAntagJobTypes => nonAntagJobTypes;


		public MainStationListSO mainStations;

		#endregion

		#region Game Mode Methods

		/// <summary>
		/// Check if the game mode meets the minimum player and antag requirements.
		/// Override this to add other checks for your game mode.
		/// </summary>
		public virtual bool IsPossible()
		{
			int players = NetworkServer.connections.Count;

			if ((players >= MinPlayers) == false )
			{
				Loggy.Info($"Failed MinPlayers with Current {players} Needed {MinPlayers} for {name}", Category.GameMode);
				return false;
			}

			if (ForceMinAntags)
			{
				if ((Math.Floor(players * antagRatio) >= MinAntags) == false)
				{
					Loggy.Info($"Failed ForceMinAntags with Current {players * antagRatio} Needed {MinAntags} for {name}", Category.GameMode);
					return false;
				}
			}

			return true;

		}

		/// <summary>
		/// Set up anything needed for the game mode before the RoundStarted event is triggered.
		/// Override this if you need any custom logic.
		/// </summary>
		public virtual void SetupRound()
		{
			Loggy.Info().Format("Setting up {0} round!", Category.GameMode, Name);
		}

		/// <summary>
		/// Checks if the conditions are met to spawn an antag, and spawns them
		/// as the antag if so, spawning them as an actual player and transferring them into the body
		/// (meaning there's no need to call PlayerSpawn.ServerSpawnPlayer). Does nothing
		/// if the conditions are not met to spawn this viewer as an antag.
		/// Only called for mid-round joiners.
		/// </summary>
		/// <param name="spawnRequest">spawn requested by the player</param>
		/// <returns>true if the viewer was spawned as an antag.</returns>
		public bool TrySpawnAntag(PlayerSpawnRequest spawnRequest)
		{
			var Antagonist = HandleRatioAndPickAntagonist(spawnRequest.Player, spawnRequest, AntagManager.Instance.AntagCount);
			if (Antagonist != null)
			{
				SpawnAntag(spawnRequest, Antagonist);
				return true;
			}

			return false;
		}

		/// <summary>
		/// Check if the joined viewer should be spawned as an antag (prior to actually
		/// spawning them).
		/// Only called for mid-round joiners.
		/// </summary>
		/// <param name="spawnRequest">player's spawn request, which should be used to determine
		/// if they should spawn as an antag</param>
		/// <returns>true if an antag should be spawned.</returns>
		protected virtual Antagonist HandleRatioAndPickAntagonist(PlayerInfo PlayerInfo, [CanBeNull] PlayerSpawnRequest spawnRequest, int NumberChosenAlready)
		{
			// Does this game mode support mid-round antags?
			if (MidRoundAntags == false)
			{
				return null;
			}

			if (spawnRequest != null)
			{
				// Can this job be an antag?
				if (NonAntagJobTypes.Contains(spawnRequest.RequestedOccupation.JobType))
				{
					return null;
				}
			}


			// Are there enough antags already?
			int newPlayerCount = NetworkServer.connections.Count + 1;
			var expectedAntagCount = Math.Min((int)Math.Round(newPlayerCount * AntagRatio), maxAntags);

			if (AntagManager.Instance.AntagCount < expectedAntagCount)
			{
				//We times the percentage based on the amount of open antag spaces
				//E.g if traitor with two open slots it will be 25 * 2 = 50% chance on spawn to get the antag
				//This prevents midround players from guessing when they can join the game to guarantee antag status
				if (expectedAntagCount - NumberChosenAlready > 1)
				{
					return PickAndCheckAntagonist(PlayerInfo, spawnRequest); //Basically we are lacking antagonist so add them
				}
				else
				{
					if (DMMath.Prob(midRoundAntagsChance))
					{
						return PickAndCheckAntagonist(PlayerInfo, spawnRequest);
					}
				}
			}

			return null;
		}

		protected virtual Antagonist PickAndCheckAntagonist(PlayerInfo PlayerInfo, [CanBeNull] PlayerSpawnRequest spawnRequest)
		{
			var CharacterSheet = PlayerInfo.RequestedCharacterSettings;

			if (spawnRequest != null)
			{
				CharacterSheet = spawnRequest.CharacterSettings;
			}


			// Has this player enabled any of the possible antags?
			if (HasPossibleAntagEnabled(ref CharacterSheet.AntagPreferences) == false
			    || HasPossibleAntagNotBanned(PlayerInfo.AccountId) == false)
			{
				return null;
			}
			return PossibleAntags.PickRandom();
		}


		/// <summary>
		/// Spawn the player requesting the spawn as an antag, includes creating their player object
		/// and transferring them to it. This is used as an alternative
		/// to PlayerSpawn.ServerSpawnPlayer when an antag should be spawned.
		///
		/// Defaults to picking a random antag from the possible antags list and
		/// spawning them as per the antag-specific spawn logic.
		/// </summary>
		protected virtual void SpawnAntag(PlayerSpawnRequest playerSpawnRequest, Antagonist Antagonist)
		{
			var antag = Antagonist;
			if (!AllocateJobsToAntags && antag.AntagOccupation == null)
			{
				Loggy.Error().Format("AllocateJobsToAntags is false but {0} AntagOccupation is null! " +
				                      "Game mode must either set AllocateJobsToAntags or possible antags neeed an AntagOccupation.",
					Category.Antags, antag.AntagName);
				return;
			}
			AntagManager.Instance.ServerSpawnAntag(antag, playerSpawnRequest);
		}

		/// <summary>
		/// Checks if the antag preferences have at least one of the possible antags enabled.
		/// Assume the antag is enabled by default if it doesn't appear in the preferences or was never set up.
		/// </summary>
		/// <param name="antagPrefs"></param>
		/// <param name="antag"></param>
		/// <returns></returns>
		protected bool HasAntagEnabled(ref AntagPrefsDict antagPrefs, Antagonist antag)
		{
			if (antag.ShowInPreferences == false)
			{
				return true;
			}
			if (antagPrefs.ContainsKey(antag.AntagName) && antagPrefs[antag.AntagName] == false)
			{
				//manually set to false by the player
				return false;
			}
			return true;
		}

		/// <summary>
		/// Checks if the antag preferences have at least one of the possible antags enabled.
		/// </summary>
		/// <param name="antagPrefs"></param>
		/// <returns></returns>
		protected bool HasPossibleAntagEnabled(ref AntagPrefsDict antagPrefs)
		{
			foreach (var antag in PossibleAntags)
			{
				if (HasAntagEnabled(ref antagPrefs, antag))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Checks if the antag preferences is not job banned.
		/// </summary>
		/// <param name="antagPrefs"></param>
		/// <returns></returns>
		protected bool HasPossibleAntagNotBanned(string userID)
		{
			foreach (var antag in PossibleAntags)
			{
				if (PlayerList.Instance.IsJobBanned(userID, antag.AntagJobType) == false)
				{
					//True if at least one of the antags can be spawned by the player
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Start the round
		/// </summary>
		public virtual void StartRound()
		{
			Loggy.Info().Format("Starting {0} round!", Category.GameMode, Name);

			List<PlayerSpawnRequest> playerSpawnRequests = new List<PlayerSpawnRequest>();
			List<Tuple<PlayerSpawnRequest, Antagonists.Antagonist>> antagSpawnRequests = new List<Tuple<PlayerSpawnRequest, Antagonist>>();;
			var jobAllocator = new JobAllocator();
			var playerPool = PlayerList.Instance.ReadyPlayers;
			AntagManager.Instance.ServerSpawnTeams();

			AntagJobAllocation(jobAllocator, playerPool, ref playerSpawnRequests, ref antagSpawnRequests);

			// Spawn all players and antags
			foreach (var spawnReq in playerSpawnRequests)
			{
				try
				{
					PlayerSpawn.NewSpawnPlayerV2(spawnReq.Player, spawnReq.RequestedOccupation,
						spawnReq.CharacterSettings);
				}
				catch (Exception e)
				{
					Loggy.Error($" Failed to spawn player {spawnReq?.Player?.Name} " + e.ToString());
				}
			}

			foreach (var spawnReq in antagSpawnRequests)
			{
				try
				{
					SpawnAntag(spawnReq.Item1, spawnReq.Item2);
				}
				catch (Exception e)
				{
					Loggy.Error($" Failed to SpawnAntag {spawnReq.Item1?.Player?.Name} Antag {spawnReq.Item1?.RequestedOccupation.OrNull()?.name}  " + e.ToString());
				}
			}


			var msg = $"{PlayerList.Instance.ReadyPlayers.Count} players ready, {antagSpawnRequests.Count} antags to spawn. {playerSpawnRequests.Count} players spawned (excludes antags), {antagSpawnRequests.Count} antags spawned";
			try
			{
				DiscordWebhookMessage.Instance.AddWebHookMessageToQueue(DiscordWebhookURLs.DiscordWebhookAdminLogURL, msg, "[GameMode]");
			}
			catch (Exception e)
			{
				Loggy.Error($" Failed to DiscordWebhookMessage Started round message " + e.ToString());
			}

			GameManager.Instance.CurrentRoundState = RoundState.Started;
			EventManager.Broadcast(Event.RoundStarted, true);
			AdminLogsManager.AddNewLog(null, $"Round {GameManager.RoundID} has started. GameMode: {gameModeName}. {msg}.", LogCategory.RoundFlow);
		}

		protected void AntagJobAllocation(JobAllocator jobAllocator, List<PlayerInfo> playerPool,
			ref List<PlayerSpawnRequest> playerSpawnRequests, ref List<Tuple<PlayerSpawnRequest, Antagonist>> antagSpawnRequests)
		{
			try
			{
				if (AllocateJobsToAntags)
				{
					// Allocate jobs to all players first then choose antags
					playerSpawnRequests = jobAllocator.DetermineJobs(playerPool);
					playerSpawnRequests.Shuffle();

					var ToRemove = new List<PlayerSpawnRequest>();

					foreach (var Request in playerSpawnRequests)
					{
						var Antagonist = HandleRatioAndPickAntagonist(Request.Player, Request, antagSpawnRequests.Count);
						if (Antagonist == null) continue;
						antagSpawnRequests.Add(new Tuple<PlayerSpawnRequest, Antagonist>(Request, Antagonist));
						ToRemove.Add(Request);
					}

					// Player and antag spawn requests are kept separate to stop players being spawned twice
					playerSpawnRequests.RemoveAll(ToRemove.Contains);
				}
				else
				{
					// Choose antags first then allocate jobs to all other players
					var ToRemove = new List<PlayerInfo>();

					foreach (var Player in playerPool)
					{
						var Antagonist = HandleRatioAndPickAntagonist(Player, null, antagSpawnRequests.Count);
						if (Antagonist == null) continue;
						antagSpawnRequests.Add(new Tuple<PlayerSpawnRequest, Antagonist>( new PlayerSpawnRequest(Player, null), Antagonist));
						ToRemove.Add(Player);
					}
					// Player and antag spawn requests are kept separate to stop players being spawned twice
					playerPool.RemoveAll(ToRemove.Contains);
					playerSpawnRequests = jobAllocator.DetermineJobs(playerPool);
				}
			}
			catch (Exception e)
			{
				Loggy.Error("Failed on Antag Job Allocation" + e.ToString());
			}
		}



		/// <summary>
		/// Calculates how many antags should be chosen at round start based on the player count.
		/// </summary>
		protected int CalculateAntagCount(int playerCount)
		{
			if (hardNumberOfAntagsToSpawn > 0) return hardNumberOfAntagsToSpawn;
			var antagCount = Math.Min((int)Math.Floor(playerCount * AntagRatio), MaxAntags);
			// If RequiresMinAntags is true then round up to MinAntags if antagCount is below
			return ForceMinAntags ? Math.Max(MinAntags, antagCount) : antagCount;
		}

		/// <summary>
		/// Check if the round should end yet
		/// </summary>
		public virtual void CheckEndCondition()
		{
			Loggy.Info("Checking end round conditions!", Category.GameMode);
		}

		/// <summary>
		/// End the round and display any relevant reports
		/// </summary>
		public virtual void EndRoundReport()
		{
			var roundDuration = GameManager.Instance.RoundTime.AddHours(-12);
			var output = $"A round has ended. Round duration: {roundDuration.ToString("HH:mm")}.";
			DiscordWebhookMessage.Instance.AddWebHookMessageToQueue(DiscordWebhookURLs.DiscordWebhookOOCURL, $"`{output}`", "");
			DiscordWebhookMessage.Instance.AddWebHookMessageToQueue(DiscordWebhookURLs.DiscordWebhookErrorLogURL, $"```{output} Total errors: {GameManager.Instance.errorCounter}. Unique errors: {GameManager.Instance.uniqueErrorCounter}```", "");
			GameManager.Instance.errorCounter = 0;
			GameManager.Instance.uniqueErrorCounter = 0;

			Loggy.Info().Format("Ending {0} round!", Category.GameMode, Name);
			try
			{
				AntagManager.Instance.ObjectiveEndAndShowAntagStatusReport();
			}
			catch (Exception e)
			{
				Loggy.Error(e.ToString());
			}

			try
			{
				StationObjectiveManager.Instance.ShowStationStatusReport();
			}
			catch (Exception e)
			{
				Loggy.Error(e.ToString());
			}


			var msg = $"The round will restart in {GameManager.Instance.RoundEndTime} seconds.";
			Chat.AddGameWideSystemMsgToChat(msg);
		}

		#endregion
	}
}
