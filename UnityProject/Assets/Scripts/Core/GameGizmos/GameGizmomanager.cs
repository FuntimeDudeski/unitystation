using System.Collections.Generic;
using System.Linq;
using Core.GameGizmos;
using Logs;
using Shared.Managers;
using UnityEngine;

namespace InGameGizmos
{
	public class GameGizmomanager : SingletonManager<GameGizmomanager>
	{
		public GameGizmoLine PrefabLineRenderer;

		public GameGizmoSprite PrefabSpriteRenderer;

		public GameGizmoText PrefabText;

		public GameGizmoSquare PrefabSquare;

		public GameGizmoBox PrefabBox;

		public List<GameGizmo> ActiveGizmos = new();

		public List<ISelectionGizmo> ActiveSelection = new();

		public GameObject SelectedObject;


		public void OnEnable()
		{
			UpdateManager.Remove(CallbackType.UPDATE, UpdateMe);
		}


		public void OnDisable()
		{
			if (UpdateManager.Instance != null)
				UpdateManager.Add(CallbackType.UPDATE, UpdateMe);
			else
				Loggy.Warning("[GameGizmoManager/OnDisable()] - Tried adding a function to the UpdateManager, " +
				              "but UpdateManager is missing! Make sure you're not calling this when loading scenes. Or Ignore this if you're exiting playmode inside the Unity Editor.");
		}

		public void UpdateMe()
		{
			foreach (var ActiveSelection in ActiveSelection) ActiveSelection.UpdateGizmos();
		}

		public static GameGizmoLine AddNewLineStaticClient(GameObject TrackingFrom, Vector3 From, GameObject TrackingTo,
			Vector3 To, Color color, float LineThickness = 0.03125f, float time = -1)
		{
			return Instance.AddNewLine(TrackingFrom, From, TrackingTo, To, color, LineThickness, time);
		}

		public static GameGizmoSprite AddNewSpriteStaticClient(GameObject Tracking, Vector3 position, Color color,
			SpriteDataSO Sprite, float time = -1)
		{
			return Instance.AddNewSprite(Tracking, position, color, Sprite, time);
		}

		public static GameGizmoText AddNewTextStaticClient(GameObject Tracking, Vector3 position, string Text,
			Color Colour, float TextSize = 3f, float time = -1)
		{
			return Instance.AddNewText(Tracking, position, Text, Colour, TextSize, time);
		}

		public static GameGizmoSquare AddNewSquareStaticClient(GameObject TrackingFrom, Vector3 Position, Color Colour,
			float LineThickness = 0.03125f, Vector2? BoxSize = null, float time = -1)
		{
			BoxSize ??= Vector2.one;
			return Instance.AddNewSquare(TrackingFrom, Position, Colour, LineThickness, BoxSize.Value, time);
		}

		public static GameGizmoBox AddNewBoxStaticClient(GameObject TrackingFrom, Vector3 Position, Color Colour,
			float BoxSize = 1, float time = -1)
		{
			return Instance.AddNewBox(TrackingFrom, Position, Colour, BoxSize, time);
		}


		public GameGizmoLine AddNewLine(GameObject TrackingFrom, Vector3 From, GameObject TrackingTo, Vector3 To,
			Color color, float LineThickness, float time = -1)
		{
			var Line = Instantiate(PrefabLineRenderer, Instance.transform);
			ActiveGizmos.Add(Line);
			Line.SetUp(TrackingFrom, From, TrackingTo, To, color, LineThickness, time);
			Line.SetTimeAlive(time);
			return Line;
		}


		public GameGizmoSprite AddNewSprite(GameObject Tracking, Vector3 position, Color color, SpriteDataSO Sprite, float time = -1)
		{
			var SpriteGizmo = Instantiate(PrefabSpriteRenderer, Instance.transform);
			ActiveGizmos.Add(SpriteGizmo);
			SpriteGizmo.SetUp(Tracking, position, color, Sprite);
			SpriteGizmo.SetTimeAlive(time);
			return SpriteGizmo;
		}

		public GameGizmoText AddNewText(GameObject Tracking, Vector3 position, string Text, Color Colour,
			float TextSize = 3f, float time = -1)
		{
			var GizmoText = Instantiate(PrefabText, Instance.transform);
			ActiveGizmos.Add(GizmoText);
			GizmoText.SetUp(Tracking, position, Text, Colour, TextSize);
			GizmoText.SetTimeAlive(time);
			return GizmoText;
		}

		public GameGizmoSquare AddNewSquare(GameObject TrackingFrom, Vector3 Position, Color Colour,
			float LineThickness, Vector2 BoxSize, float time = -1)
		{
			var GizmoSquare = Instantiate(PrefabSquare, Instance.transform);
			ActiveGizmos.Add(GizmoSquare);
			GizmoSquare.SetUp(TrackingFrom, Position, Colour, LineThickness, BoxSize);
			GizmoSquare.SetTimeAlive(time);
			return GizmoSquare;
		}

		public GameGizmoBox AddNewBox(GameObject TrackingFrom, Vector3 Position, Color Colour, float BoxSize = 1, float time = -1)
		{
			var GizmoBox = Instantiate(PrefabBox, Instance.transform);
			ActiveGizmos.Add(GizmoBox);
			GizmoBox.SetUp(TrackingFrom, Position, Colour, BoxSize);
			GizmoBox.SetTimeAlive(time);
			return GizmoBox;
		}


		public static void SelectObject(GameObject Object)
		{
			if (Instance.SelectedObject != null) UnSelectObject(Instance.SelectedObject);

			Instance.SelectedObject = Object;

			var Gizmos = Object.GetComponents<ISelectionGizmo>().ToList();
			foreach (var Gizmo in Gizmos) Gizmo.OnSelected();

			Instance.ActiveSelection.AddRange(Gizmos);
		}

		public static void UnSelectObject(GameObject Object)
		{
			if (Object == null)
			{
				foreach (var Gizmo in Instance.ActiveSelection) Gizmo.OnDeselect();
				Instance.ActiveSelection.Clear();
			}
			else
			{
				var Gizmos = Object.GetComponents<ISelectionGizmo>().ToList();
				foreach (var Gizmo in Gizmos)
				{
					Gizmo.OnDeselect();
					Instance.ActiveSelection.Remove(Gizmo);
				}
			}
		}
	}
}