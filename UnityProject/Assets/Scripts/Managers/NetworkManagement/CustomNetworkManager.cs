using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core;
using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using DatabaseAPI;
using IgnoranceTransport;
using Initialisation;
using Logs;
using MapSaver;
using Messages.Server;
using SecureStuff;
using UnityEditor;
using Util;
using Object = UnityEngine.Object;
using UniversalObjectPhysics = Core.Physics.UniversalObjectPhysics;

public class CustomNetworkManager : NetworkManager, IInitialise
{

	// NetworkManager.isHeadless is removed in latest versions of Mirror,
	// so we assume headless would be running in batch mode.
	public static bool IsHeadless
	{
		get
		{
#if UNITY_STANDALONE_LINUX_API
			return true;
#endif
			return Application.isBatchMode;
		}
	}

	public static CustomNetworkManager Instance;

	[NonSerialized] public bool SpawnedByMappingTool = false;

	public List<GameObject> NetworkedManagersPrefabs;

	public List<GameObject> ActiveNetworkedManagersPrefabs;

	public static bool IsServer;
	[HideInInspector] private ServerConfig config;
	public GameObject humanPlayerPrefab;
	public GameObject ghostPrefab;
	public GameObject disconnectedViewerPrefab;

	/// <summary>
	/// List of ALL prefabs in the game which can be spawned, networked or not.
	/// use spawnPrefabs to get only networked prefabs
	/// </summary>
	[HideInInspector] public List<GameObject> allSpawnablePrefabs = new();


	public Dictionary<GameObject, int> IndexLookupSpawnablePrefabs = new();

	public Dictionary<string, GameObject> ForeverIDLookupSpawnablePrefabs = new();

	/// <summary>
	/// Invoked client side when the player has disconnected from a server.
	/// </summary>
	[NonSerialized] public UnityEvent OnClientDisconnected = new UnityEvent();

	public static Dictionary<uint, NetworkIdentity> Spawned => IsServer ? NetworkServer.spawned : NetworkClient.spawned;

	private int currentLocation = 0;

	public Dictionary<uint, Tuple<MapSaver.MapSaver.PrefabData, Matrix>> PrePayload =
		new Dictionary<uint, Tuple<MapSaver.MapSaver.PrefabData, Matrix>>();

	public List<Tuple<string, int>> LoadedMapDatas = new List<Tuple<string, int>>();

	public static bool AllPrefabsLoadedSt => Instance.AllPrefabsLoaded;
	public bool AllPrefabsLoaded => allSpawnablePrefabs.Count <= currentLocation;

	public void UpdateMe()
	{
		if (AllPrefabsLoaded == false)
		{
			for (int i = 0; i < 50; i++)
			{
				if (allSpawnablePrefabs.Count > currentLocation + i)
				{
					if (allSpawnablePrefabs[currentLocation + i] == null) continue;
					if (allSpawnablePrefabs[currentLocation + i].TryGetComponent<PrefabTracker>(out var PrefabTracker))
					{
						ForeverIDLookupSpawnablePrefabs[PrefabTracker.ForeverID] =
							allSpawnablePrefabs[currentLocation + i];
					}
				}
			}

			currentLocation = currentLocation + 50;
		}
	}


	public void ReceiveMattOverrides(MapSaver.MapSaver.CompactObjectMapData data, bool DoStraightaway, int MatrixID)
	{
		if (data == null) return;
		var Matrix = MatrixManager.Get(MatrixID);

		foreach (var PD in data.PrefabData)
		{
			var id = PD.GitID;
			if (string.IsNullOrEmpty(id))
			{
				id = PD.ID.ToString();
			}


			PrePayload[data.IDToNetIDClient[id]] = new Tuple<MapSaver.MapSaver.PrefabData, Matrix>(PD, Matrix.Matrix);
		}

		if (DoStraightaway)
		{
			foreach (var PD in data.PrefabData)
			{
				var id = PD.GitID;
				if (string.IsNullOrEmpty(id))
				{
					id = PD.ID.ToString();
				}

				if (Spawned.TryGetValue(data.IDToNetIDClient[id], out var networkIdentity))
				{
					ObjectBeforePayloadDataClient(networkIdentity);
				}
			}
		}
	}


	public override void ObjectBeforePayloadDataClient(NetworkIdentity identity) //NOTE : Won't handle object to object references,
		//However these should be synchronised By mirror since I can't Think of a state where they won't be
	{
		try
		{
			if (IsServer) return;
			if (PrePayload.TryGetValue(identity.netId, out var prefabdata))
			{
				MapLoader.ProcessIndividualObject(null, prefabdata.Item1, prefabdata.Item2, Vector3Int.zero,
					Vector3Int.zero, identity.gameObject);
				foreach (var Spawn in identity.GetComponentsInChildren<INewMappedOnSpawn>())
				{
					Spawn.OnNewMappedOnSpawn();
				}
			}
		}
		catch (Exception e)
		{
			Loggy.Error(e.ToString());
		}
	}

	public void Clear()
	{
		Debug.Log("removed " +
		          CleanupUtil.RidDictionaryOfDeadElements(IndexLookupSpawnablePrefabs, (u, k) => u != null) +
		          " dead elements from CustomNetworkManager.IndexLookupSpawnablePrefabs");

		foreach (var a in IndexLookupSpawnablePrefabs)
		{
			TileManager tm = a.Key.GetComponent<TileManager>();

			if (tm != null)
			{
				tm.DeepCleanupTiles();
			}
		}
	}


	public override void Awake()
	{
		bool Maped = false;
#if UNITY_EDITOR
		if (Instance != null)
		{
			Maped = Instance.SpawnedByMappingTool;
		}

#endif

		EventManager.AddHandler(Event.SceneUnloading, RoundEndingClientNServer);
		if (Instance == null || Instance == this || Maped)
		{
			Instance = this;
		}
		else
		{
			Destroy(gameObject);
			return;
		}


		if (IndexLookupSpawnablePrefabs.Count == 0)
		{
			new Task(SetUpSpawnablePrefabsIndex).Start();
		}
	}

	public void SetUpSpawnablePrefabsIndex()
	{
		for (int i = 0; i < allSpawnablePrefabs.Count; i++)
		{
			IndexLookupSpawnablePrefabs[allSpawnablePrefabs[i]] = i;
		}
	}

	public void SetUpSpawnablePrefabsForEverIDManual()
	{
		for (int i = 0; i < allSpawnablePrefabs.Count; i++)
		{
			if (allSpawnablePrefabs[i] == null) continue;
			var prefabTracker = allSpawnablePrefabs[i].GetComponent<PrefabTracker>();
			if (prefabTracker == null) continue;
			ForeverIDLookupSpawnablePrefabs[prefabTracker.ForeverID] = allSpawnablePrefabs[i];
		}
	}

	public InitialisationSystems Subsystem => InitialisationSystems.CustomNetworkManager;

	void IInitialise.Initialise()
	{
		ApplyConfig();

		var prevEditorScene = SubSceneManager.GetEditorPrevScene();
		if (prevEditorScene != string.Empty && prevEditorScene != "StartUp" && prevEditorScene != "Lobby")
		{
			StartHostWrapper();
		}
	}

	public void StartHostWrapper()
	{
		StartHost();
		EventManager.AddHandler(Event.SceneUnloading, RoundEnding);
		EventManager.AddHandler(Event.ScenesLoadedServer, InitNetworkedManagers);
	}

	public void InitNetworkedManagers()
	{
#if UNITY_EDITOR
		//Opening closing shenanigans
		if ( this == null)
		{
			Instance = FindObjectsByType<CustomNetworkManager>(FindObjectsSortMode.None)[0];
			Instance.InitNetworkedManagers();
			return;
		}
#endif

		StartCoroutine(WaitForInit());
	}

	private IEnumerator WaitForInit()
	{
		yield return WaitFor.Seconds(1f);
		foreach (var NetworkedManagersPrefab in NetworkedManagersPrefabs)
		{
			var spawnedObject = Object.Instantiate(NetworkedManagersPrefab, Vector3.zero, Quaternion.identity,
				this.gameObject.transform);
			NetworkServer.Spawn(spawnedObject);
			ActiveNetworkedManagersPrefabs.Add(spawnedObject);
		}
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
		if (Instance == this)
		{
			Instance = null;
		}
	}

	public void RoundEnding()
	{
		foreach (var NetworkedManagersPrefab in ActiveNetworkedManagersPrefabs)
		{
			NetworkServer.Destroy(NetworkedManagersPrefab);
		}

		ActiveNetworkedManagersPrefabs.Clear();
	}

	public void RoundEndingClientNServer()
	{
		PrePayload.Clear();
		LoadedMapDatas.Clear();
		MapSaver.MapSaver.CodeClass.ThisCodeClass.Reset();
	}

	void ApplyConfig()
	{
		config = ServerData.ServerConfig;
		if (config.ServerPort != 0 && config.ServerPort <= 65535)
		{
			Loggy.Info().Format("ServerPort defined in config: {0}", Category.Server, config.ServerPort);
			// var booster = GetComponent<BoosterTransport>();
			// if (booster != null)
			// {
			// 	booster.port = (ushort)config.ServerPort;
			// }
			// else
			// {
			//
			// }

			var telepathy = GetComponent<TelepathyTransport>();
			if (telepathy != null)
			{
				telepathy.port = (ushort) config.ServerPort;
			}

			var ignorance = GetComponent<Ignorance>();
			if (ignorance != null)
			{
				ignorance.port = (ushort) config.ServerPort;
			}
		}

		if (string.IsNullOrEmpty(config.BindAddress) == false)
		{
			var ignorance = GetComponent<Ignorance>();
			if (ignorance != null)
			{
				ignorance.serverBindsAll = false;
				ignorance.serverBindAddress = config.BindAddress;
			}
		}
	}

	[ContextMenu("Print network server")]
	public void PrintNetworkServer()
	{
		Loggy.Error(NetworkServer.spawned.Count.ToString());
	}

	[ContextMenu("Print network client")]
	public void PrintNetworkClient()
	{
		Loggy.Error(NetworkClient.spawned.Count.ToString());
	}

	public void SetSpawnableList()
	{
#if UNITY_EDITOR
		AssetDatabase.StartAssetEditing();
		spawnPrefabs.Clear();
		allSpawnablePrefabs.Clear();

		var storedIDs = new Dictionary<string, PrefabTracker>();

		var networkObjectsGUIDs = AssetDatabase.FindAssets("t:prefab", new string[] {"Assets/Prefabs"});
		var objectsPaths = networkObjectsGUIDs.Select(AssetDatabase.GUIDToAssetPath);
		foreach (var objectsPath in objectsPaths)
		{
			var asset = AssetDatabase.LoadAssetAtPath<GameObject>(objectsPath);
			if (asset == null) continue;

			if (asset.TryGetComponent<NetworkIdentity>(out _) && playerPrefab != asset)
			{
				spawnPrefabs.Add(asset);
			}

			allSpawnablePrefabs.Add(asset);

			if (asset.TryGetComponent<PrefabTracker>(out var prefabTracker))
			{
				if (storedIDs.ContainsKey(prefabTracker.ForeverID))
				{
					var originalOldID = prefabTracker.ForeverID;

					var originDictionary =
						PrefabUtility.GetCorrespondingObjectFromSource(storedIDs[prefabTracker.ForeverID].gameObject);
					if (originDictionary == prefabTracker.gameObject)
					{
						storedIDs[prefabTracker.ForeverID].ReassignID();
					}
					else
					{
						prefabTracker.ReassignID();
					}

					var preexisting = storedIDs[originalOldID];

					if (preexisting.ForeverID != originalOldID &&
					    prefabTracker.ForeverID != originalOldID)
					{
						Loggy.Error("OH GOD What is the original I can't tell!! " +
						            "Manually edit the ForeverID For the newly created prefab to not be the same as " +
						            "the prefab variant parent for " +
						            preexisting.gameObject +
						            " and " + prefabTracker.gameObject);

						prefabTracker.ForeverID = originalOldID;
						preexisting.ForeverID = originalOldID;
						continue;
					}


					storedIDs[preexisting.ForeverID] = preexisting;
					storedIDs[prefabTracker.ForeverID] = prefabTracker;
					PrefabUtility.SavePrefabAsset(preexisting.gameObject);
					PrefabUtility.SavePrefabAsset(prefabTracker.gameObject);
				}

				storedIDs[prefabTracker.ForeverID] = prefabTracker;
			}
		}

		AssetDatabase.StopAssetEditing();
		AssetDatabase.SaveAssets();
#endif
	}

	public GameObject GetSpawnablePrefabFromName(string prefabName)
	{
#if UNITY_EDITOR
		//Opening closing shenanigans
		if (allSpawnablePrefabs == null || this == null)
		{
			Instance = FindObjectsByType<CustomNetworkManager>(FindObjectsSortMode.None)[0];
			return	Instance.GetSpawnablePrefabFromName(prefabName);
		}
#endif
		var prefab = allSpawnablePrefabs?.Where(o => o?.name == prefabName).ToList();

		if (prefab != null && prefab.Any())
		{
			if (prefab.Count > 1)
			{
				Loggy.Error($"There is {prefab.Count} prefabs with the name: {prefabName}, please rename them");
			}

			return prefab[0];
		}

		Loggy.Error(
			$"There is no prefab with the name: {prefabName} inside the AllSpawnablePrefabs list in the network manager," +
			" all prefabs must be in this list if they need to be spawnable");

		return null;
	}

	private void OnEnable()
	{
		SceneManager.activeSceneChanged += OnLevelFinishedLoading;
		UpdateManager.Add(CallbackType.UPDATE, UpdateMe);
	}

	private void OnDisable()
	{
		SceneManager.activeSceneChanged -= OnLevelFinishedLoading;
		UpdateManager.Remove(CallbackType.UPDATE, UpdateMe);
	}

	public override void OnStartServer()
	{
		IsServer = true;
		base.OnStartServer();
		NetworkManagerExtensions.RegisterServerHandlers();
		// Fixes loading directly into the station scene
		GameManager.Instance.PreRoundStart();
	}

	public override void OnStartHost()
	{
		StartCoroutine(WaitForInitialisation());
	}

	public IEnumerator WaitForInitialisation()
	{
		yield return null;
		yield return null;
		yield return null;
		AddressableCatalogueManager.LoadHostCatalogues();
	}

	public override void OnStartClient()
	{
		if (AddressableCatalogueManager.Instance == null) return;

		AddressableCatalogueManager.Instance.LoadClientCatalogues();
	}

	//called on server side when player is being added, this is the main entry point for a client connecting to this server
	public override void OnServerAddPlayer(NetworkConnectionToClient conn)
	{
		if (IsHeadless || GameData.Instance.testServer)
		{
			if (conn == NetworkServer.localConnection)
			{
				Loggy.Info("Prevented headless server from spawning a player", Category.Connections);
				return;
			}
		}

		Loggy.Trace($"Spawning a GameObject for the client {conn}.", Category.Connections);
		base.OnServerAddPlayer(conn);
		SubSceneManager.Instance.AddNewObserverScenePermissions(conn);
		UpdateRoundTimeMessage.Send(GameManager.Instance.RoundTime.ToString("O"),
			GameManager.Instance.RoundTimeInMinutes);
	}

	//called on client side when client first connects to the server
	public override void OnClientConnect()
	{
		//Does this need to happen all the time? OnClientConnect can be called multiple times
		NetworkManagerExtensions.RegisterClientHandlers();

		base.OnClientConnect();
	}

	public override void OnClientDisconnect()
	{
		Loggy.Info("Client disconnected from the server.");
		base.OnClientDisconnect();
		OnClientDisconnected.Invoke();
	}

	public override void OnServerConnect(NetworkConnectionToClient conn)
	{
		// Connection has been authenticated via Authentication.cs
		Loggy.Trace($"A client has been authenticated and has joined. Address: {conn.address}.");

		base.OnServerConnect(conn);
	}

	/// server actions when client disconnects
	public override void OnServerDisconnect(NetworkConnectionToClient conn)
	{
		Loggy.Error($"Disconnecting {conn.address}");
		//register them as removed from our own player list

		PlayerList.Instance.RemoveByConnection(conn);

		foreach (var ownedObject in conn.owned.ToArray())
		{
			if (ownedObject.connectionToClient?.identity == ownedObject) continue;
			ownedObject.RemoveClientAuthority();
		}

		//now we can call mirror's normal disconnect logic, which will destroy all the player's owned objects
		base.OnServerDisconnect(conn);
		SubSceneManager.Instance.RemoveSceneObserver(conn);
	}

	private void OnLevelFinishedLoading(Scene oldScene, Scene newScene)
	{
		if (newScene.name != "Lobby")
		{
			//INGAME:
			// TODO check if this is needed
			// EventManager.Broadcast(EVENT.RoundStarted);
			StartCoroutine(DoHeadlessCheck());
		}
		else
		{
			// must've disconnected, let lobby know (now that scene is loaded)
			Lobby.LobbyManager.Instance.WasDisconnected = true;
		}
	}

	private IEnumerator DoHeadlessCheck()
	{
		yield return WaitFor.Seconds(0.1f);
		if (GameData.IsHeadlessServer && GameData.Instance.testServer)
		{
			//Set up for headless mode stuff here
			//Useful for turning on and off components
			IsServer = true;
		}
	}

	public override void OnApplicationQuit()
	{
		base.OnApplicationQuit();
		// stop transport (e.g. to shut down threads)
		// (when pressing Stop in the Editor, Unity keeps threads alive
		//  until we press Start again. so if Transports use threads, we
		//  really want them to end now and not after next start)
		var transportComponent = GetComponent<Transport>();
		transportComponent.Shutdown();
	}

	//Editor item transform dance experiments
#if UNITY_EDITOR
	public void MoveAll()
	{
		StartCoroutine(TransformWaltz());
	}

	private IEnumerator TransformWaltz()
	{
		UniversalObjectPhysics[] scripts = FindObjectsOfType<UniversalObjectPhysics>();
		var sequence = new[]
		{
			Vector3.right, Vector3.up, Vector3.left, Vector3.down,
			Vector3.down, Vector3.left, Vector3.up, Vector3.right
		};
		for (var i = 0; i < sequence.Length; i++)
		{
			for (var j = 0; j < scripts.Length; j++)
			{
				NudgeTransform(scripts[j], sequence[i]);
			}

			yield return WaitFor.Seconds(1.5f);
		}
	}

	private static void NudgeTransform(UniversalObjectPhysics objectPhysics, Vector3 where)
	{
		objectPhysics.AppearAtWorldPositionServer(objectPhysics.OfficialPosition + where);
	}
#endif
}