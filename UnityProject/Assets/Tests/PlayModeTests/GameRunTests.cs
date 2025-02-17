using System.Collections;
using Logs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GameRunTests
{
	public class GameRunTests
	{
		[Test]
		public IEnumerator NewTestScriptWithEnumeratorPasses()
		{
			yield return SceneManager.LoadSceneAsync("OnlineScene");

			if (GameManager.Instance == null)
			{
				Loggy.Error("Unable to load OnlineScene Properly returning");
				yield break;
			}
			GameManager.Instance.QuickLoad = true;

			yield return TestSingleton.Instance.RunTests();

			GameManager.Instance.QuickLoad = false;
		}

		public static void RunRestartRound()
		{
			GameManager.Instance.RoundEndTime = 0f;
			GameManager.Instance.EndRound(GameManager.RoundID);
		}

		// public void RunRestartRound()
		// {
		// 	if (CustomNetworkManager.IsServer == false)
		// 	{
		// 		Loggy.Log("Can only execute command from server.", Category.DebugConsole);
		// 		return;
		// 	}
		//
		// 	Loggy.Log("Triggered round restart from DebugConsole.", Category.DebugConsole);
		// 	GameManager.Instance.RoundEndTime = 1f;
		// 	GameManager.Instance.EndRound();
		// }
	}
}
