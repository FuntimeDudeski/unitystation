﻿using System;
using System.Collections;
using System.Collections.Generic;
using Chemistry.Components;
using UnityEngine;
using Mirror;
using Random = UnityEngine.Random;

namespace Objects.Construction
{
	/// <summary>
	/// Represents some decal that goes on the floor and can potentially be cleaned up by
	/// janitorial actions. Decal can have random variations in its sprite among other
	/// capabilities.
	/// </summary>
	public class FloorDecal : NetworkBehaviour
	{
		/// <summary>
		/// Whether this decal can be cleaned up by janitorial actions like mopping.
		/// </summary>
		[Tooltip("Whether this decal can be cleaned up by janitorial actions like mopping.")]
		public bool Cleanable = true;

		public bool CanDryUp = false;

		public bool isBlood = false;

		public bool IsFootprint = false;

		public bool IsSlippery = false;
		public bool IsSuperSlippery = false;

		[SyncVar(hook = "OnColorChanged")] [HideInInspector]
		public Color color;

		[Tooltip("Possible appearances of this decal. One will randomly be chosen when the decal appears." +
		         " This can be left empty, in which case the prefab's sprite renderer sprite will " +
		         "be used.")]
		public Sprite[] PossibleSprites;

		//public SpriteHandler FootPrints;

		[SyncVar(hook = nameof(SyncChosenSprite))]
		private int chosenSprite;

		private SpriteRenderer spriteRenderer;

		public bool DontTouchSpriteRenderer = false;

		private ReagentContainer reagentContainer;
		public ReagentContainer ReagentContainer => reagentContainer;


		private void Awake()
		{
			EnsureInit();
		}

		private void EnsureInit()
		{
			reagentContainer ??= GetComponent<ReagentContainer>();
			if (spriteRenderer != null) return;
			spriteRenderer = GetComponentInChildren<SpriteRenderer>();
		}

		public override void OnStartServer()
		{
			EnsureInit();
			//randomly pick if there are options
			if (PossibleSprites != null && PossibleSprites.Length > 0)
			{
				chosenSprite = Random.Range(0, PossibleSprites.Length);
			}
		}

		public override void OnStartClient()
		{
			EnsureInit();
			SyncChosenSprite(chosenSprite, chosenSprite);
		}

		public void Start()
		{
			if (CustomNetworkManager.IsServer && CanDryUp)
			{
				StartCoroutine(DryUp());
			}
		}

		private IEnumerator DryUp()
		{
			yield return WaitFor.Seconds(Random.Range(10, 21));

			var Matrix = MatrixManager.AtPoint(transform.position, isServer);
			var Node = Matrix.MetaDataLayer.Get(transform.position.ToLocalInt(Matrix));

			if (IsSuperSlippery)
			{
				Node.IsSuperSlippery = false;
			}

			if (IsSlippery)
			{
				Node.IsSlippery = false;
			}
			
			_ = Despawn.ServerSingle(this.gameObject);
		}

		private void SyncChosenSprite(int _oldSprite, int _chosenSprite)
		{
			EnsureInit();
			chosenSprite = _chosenSprite;
			if (PossibleSprites != null && PossibleSprites.Length > 0 && DontTouchSpriteRenderer == false)
			{
				spriteRenderer.sprite = PossibleSprites[chosenSprite];
			}
		}

		public void OnColorChanged(Color oldColor, Color newColor)
		{
			if (spriteRenderer && DontTouchSpriteRenderer == false)
			{
				spriteRenderer.color = newColor;
			}
		}

		/// <summary>
		///attempts to clean this decal, cleaning it if it is cleanable
		/// </summary>
		public void TryClean()
		{
			if (Cleanable)
			{
				_ = Despawn.ServerSingle(gameObject);
			}
		}
	}
}