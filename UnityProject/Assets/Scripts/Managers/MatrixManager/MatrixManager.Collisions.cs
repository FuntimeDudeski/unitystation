using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core;
using UnityEngine;
using UnityEngine.Events;
using Systems.Electricity;
using Systems.Electricity.Inheritance;
using Systems.Electricity.NodeModules;
using Systems.Explosions;
using HealthV2;
using Logs;
using Random = UnityEngine.Random;
using Objects.Electrical;
using Objects.Engineering;
using TileManagement;
using UniversalObjectPhysics = Core.Physics.UniversalObjectPhysics;

/// <summary>
/// Collision-related stuff
/// </summary>
public partial class MatrixManager
{
	private const float EXTRA_COLLISION_THRESHOLD = 200f;

	/// <summary>
	/// Currently moving matrices. Should be monitored for new intersections with other matrix bounds
	/// </summary>
	private List<MatrixInfo> movingMatrices = new List<MatrixInfo>();

	/// <summary>
	/// bounds intersections that should be actively checked for actual tile collisions
	/// </summary>
	private List<MatrixIntersection> trackedIntersections = new List<MatrixIntersection>();

	public List<MatrixIntersection> TrackedIntersections => trackedIntersections;

	private static LayerType[] layersToRemove = { LayerType.Effects };
	private static LayerType[] effectsToRemove = { LayerType.Effects, LayerType.Grills, LayerType.Floors};

	private void InitCollisions(MatrixInfo matrixInfo)
	{
		if (!Application.isPlaying || !CustomNetworkManager.IsServer)
		{
			return;
		}

		if (matrixInfo!= null && matrixInfo.IsMovable)
		{
			matrixInfo.MatrixMove.NetworkedMatrixMove.OnStartMovement += ( () =>
			{
				if ( !movingMatrices.Contains( matrixInfo ) )
				{
					movingMatrices.Add( matrixInfo );
				}
			} );

			matrixInfo.MatrixMove.NetworkedMatrixMove.OnStopMovement += ( () =>
			{
				if ( movingMatrices.Contains( matrixInfo ) )
				{
					var participatingIntersections = trackedIntersections.FindAll( intersection => intersection.Matrix1 == matrixInfo );
					CollideBeforeStop( matrixInfo, participatingIntersections );
					movingMatrices.Remove( matrixInfo );
					trackedIntersections.RemoveAll( intersection => intersection.Matrix1 == matrixInfo );
				}
			} );
		}

		UnityAction CollideBeforeStop( MatrixInfo matrix, List<MatrixIntersection> intersections )
		{
			return () =>
			{
				foreach ( var intersection in intersections )
				{
					CheckTileCollisions( intersection );
				}
				intersections.Clear();
			};
		}
	}

	private void RefreshIntersections()
	{
		if ( movingMatrices.Count == 0 )
		{
			if ( trackedIntersections.Count > 0 )
			{
				trackedIntersections.Clear();
			}
			return;
		}

		UpdateAndPruneIntersections();
		TrackNewIntersections();

		void UpdateAndPruneIntersections()
		{
			List<MatrixIntersection> toRemove = null;
			List<MatrixIntersection> toUpdate = null;

			foreach ( var trackedIntersection in trackedIntersections )
			{
				if ( trackedIntersection.Matrix1.BoundsIntersect( trackedIntersection.Matrix2, out var hotZone ) )
				{ //refresh rect
					if ( toUpdate == null )
					{
						toUpdate = new List<MatrixIntersection>();
					}

					toUpdate.Add(trackedIntersection.Clone(ref hotZone));
				}
				else
				{ //stop tracking non-intersecting ones
					if ( toRemove == null )
					{
						toRemove = new List<MatrixIntersection>();
					}

					toRemove.Add( trackedIntersection );
				}
			}

			if ( toUpdate != null )
			{
				foreach ( var updateMe in toUpdate )
				{
					trackedIntersections.Remove( updateMe );
					trackedIntersections.Add( updateMe );
				}
			}

			if ( toRemove != null )
			{
				foreach ( var removeMe in toRemove )
				{
					trackedIntersections.Remove( removeMe );
				}
			}

		}

		void TrackNewIntersections()
		{
			foreach ( var movingMatrix in movingMatrices )
			{
				var intersections = GetIntersections( movingMatrix );
				if ( intersections == noIntersections )
				{
					continue;
				}

				foreach ( var intersection in intersections )
				{
					if ( trackedIntersections.Contains( intersection ) )
					{
						continue;
					}

					trackedIntersections.Add( intersection );
				}
			}
		}
	}

	private static readonly MatrixIntersection[] noIntersections = new MatrixIntersection[0];

	private MatrixIntersection[] GetIntersections( MatrixInfo matrix )
	{
		List<MatrixIntersection> intersections = null;
		foreach ( var otherMatrix in ActiveMatrices.Values )
		{
			if ( matrix == null || matrix == otherMatrix )
			{
				continue;
			}
			if ( matrix.BoundsIntersect( otherMatrix, out BetterBounds hotZone ) )
			{
				if ( intersections == null )
				{
					intersections = new List<MatrixIntersection>();
				}

				intersections.Add(new MatrixIntersection(matrix, otherMatrix, hotZone));
			}
		}

		if ( intersections != null )
		{
			return intersections.ToArray();
		}
		return noIntersections;
	}

	private void UpdateMe()
	{
		if (!CustomNetworkManager.IsServer)
		{
			return;
		}

		RefreshIntersections();

		if ( trackedIntersections.Count == 0 )
		{
			return;
		}

		for ( var i = trackedIntersections.Count - 1; i >= 0; i-- )
		{
			if ( trackedIntersections.Count - 1 < i )
			{
				continue;
			}
			CheckTileCollisions( trackedIntersections[i] );
		}
	}

	private static void MatrixSafetyCheck(MatrixInfo Matrix1)
	{
		if (Matrix1.MatrixMove.NetworkedMatrixMove.Safety)
		{

			if (Matrix1.MatrixMove.NetworkedMatrixMove.TargetOrientation != OrientationEnum.Default)
			{
				var StartOrientation = Matrix1.MatrixMove.NetworkedMatrixMove.StartOrientation;
				Matrix1.MatrixMove.NetworkedMatrixMove.TargetOrientation = Matrix1.MatrixMove.NetworkedMatrixMove.StartOrientation;
				Matrix1.MatrixMove.NetworkedMatrixMove.StartOrientation = StartOrientation;
			}


			var addmove =  Matrix1.MatrixMove.NetworkedMatrixMove.WorldCurrentVelocity.normalized * -1;
			Matrix1.MatrixMove.NetworkedMatrixMove.TargetTransform.position += addmove;

			Matrix1.MatrixMove.NetworkedMatrixMove.WorldCurrentVelocity *= 0;
			Matrix1.MatrixMove.NetworkedMatrixMove.MoveCoolDown = 3;


			foreach (var Thruster in Matrix1.MatrixMove.NetworkedMatrixMove.ConnectedThrusters)
			{
				Thruster.SetTargetMolesUsed(0);
			}
			return;
		}
	}


	private static void MatrixSpeedCheck(MatrixInfo Matrix1, MatrixInfo Matrix2)
	{
		if (Matrix2.MatrixMove.NetworkedMatrixMove.WorldCurrentVelocity.magnitude < 4
		    && Matrix1.MatrixMove.NetworkedMatrixMove.WorldCurrentVelocity.magnitude < 4)
		{
			if (Matrix1.MatrixMove.NetworkedMatrixMove.DragSpinneyCoolDown > 0 == false &&
			    Matrix2.MatrixMove.NetworkedMatrixMove.DragSpinneyCoolDown > 0 == false)
			{
				Matrix2.MatrixMove.NetworkedMatrixMove.WorldCurrentVelocity *= -1;
				Matrix1.MatrixMove.NetworkedMatrixMove.WorldCurrentVelocity *= -1;

				foreach (var Thruster in Matrix2.MatrixMove.NetworkedMatrixMove.ConnectedThrusters)
				{
					Thruster.SetTargetMolesUsed(0);
				}

				foreach (var Thruster in Matrix1.MatrixMove.NetworkedMatrixMove.ConnectedThrusters)
				{
					Thruster.SetTargetMolesUsed(0);
				}
			}

		}

	}

	private static List<Vector3Int> collisionLocations = new List<Vector3Int>();


	private void CheckTileCollisions( MatrixIntersection i )
	{
		if (i.Matrix1 == null || i.Matrix2 == null) return;
		if (i.Matrix1.MatrixMove.NetworkedMatrixMove.IsConnectedToShuttle(i.Matrix2.MatrixMove.NetworkedMatrixMove))
		{
			return;
		}


		byte collisions = 0;
		foreach ( var worldPos in i.Rect.allPositionsWithin() )
		{

			Vector3Int cellPos1 = i.Matrix1.MetaTileMap.WorldToCell( worldPos );

			var Meta1 = i.Matrix1.Matrix.MetaTileMap;
			if ( Meta1.HasTile( cellPos1, LayerType.Base ) == false)
			{
				continue;
			}

			Vector3Int cellPos2 = i.Matrix2.MetaTileMap.WorldToCell( worldPos );
			var Meta2 = i.Matrix2.Matrix.MetaTileMap;
			if ( Meta2.HasTile( cellPos2, LayerType.Base ) == false)
			{
				continue;
			}

			float resistance1 = i.Matrix1.MetaTileMap.Resistance( cellPos1, true );
			if ( resistance1 <= 0f )
			{
				continue;
			}

			float resistance2 = i.Matrix2.MetaTileMap.Resistance( cellPos2, true );
			if ( resistance2 <= 0f )
			{
				continue;
			}



			MatrixSafetyCheck(i.Matrix1);

			MatrixSafetyCheck(i.Matrix2);

			MatrixSpeedCheck(i.Matrix1, i.Matrix2);

			i.Matrix2.MatrixMove.NetworkedMatrixMove.DragSpinneyCoolDown = 3;
			i.Matrix1.MatrixMove.NetworkedMatrixMove.DragSpinneyCoolDown = 3;
			collisionLocations.Add( worldPos );

			//
			// ******** DESTROY STUFF!!! ********
			//

			//total damage to apply to victim tile
			if ( resistance1 + (10 * i.Matrix1.Speed) >= resistance2 + (10 * i.Matrix2.Speed) )
			{
				//attacker tile is stronger:

				//destroy victim tile
				TryPushing( i.Matrix2, cellPos2, i.Matrix1.MovementVector, i.Matrix1.Speed );
				ApplyCritDamage( i.Matrix2, cellPos2, worldPos );

				//slightly damage adjacent victim tiles
				ApplyTilemapDamage( i.Matrix2, cellPos2+Vector3Int.up, resistance1*0.4f, worldPos );
				ApplyTilemapDamage( i.Matrix2, cellPos2+Vector3Int.down, resistance1*0.4f, worldPos );
				ApplyTilemapDamage( i.Matrix2, cellPos2+Vector3Int.left, resistance1*0.4f, worldPos );
				ApplyTilemapDamage( i.Matrix2, cellPos2+Vector3Int.right, resistance1*0.4f, worldPos );

				//damage back attacker
				TryPushing( i.Matrix1, cellPos1, i.Matrix2.MovementVector, i.Matrix2.Speed );
				ApplyDamage( i.Matrix1,	cellPos1, resistance2, worldPos );
				if ( resistance2 > EXTRA_COLLISION_THRESHOLD )
				{
					collisions += (byte)( (resistance2-EXTRA_COLLISION_THRESHOLD) / EXTRA_COLLISION_THRESHOLD );
				}

			} else
			{
				//victim tile is stronger

				//destroy weaker tile
				TryPushing( i.Matrix1, cellPos1, i.Matrix2.MovementVector, i.Matrix2.Speed );
				ApplyCritDamage( i.Matrix1,	cellPos1, worldPos );

				//slightly damage adjacent tiles
				ApplyTilemapDamage( i.Matrix1, cellPos1+Vector3Int.up, resistance2*0.4f, worldPos );
				ApplyTilemapDamage( i.Matrix1, cellPos1+Vector3Int.down, resistance2*0.4f, worldPos );
				ApplyTilemapDamage( i.Matrix1, cellPos1+Vector3Int.left, resistance2*0.4f, worldPos );
				ApplyTilemapDamage( i.Matrix1, cellPos1+Vector3Int.right, resistance2*0.4f, worldPos );

				//damage back
				TryPushing( i.Matrix2, cellPos2, i.Matrix1.MovementVector, i.Matrix1.Speed );
				ApplyDamage( i.Matrix2, cellPos2, resistance1, worldPos );
				if ( resistance1 > EXTRA_COLLISION_THRESHOLD )
				{
					collisions += (byte)( (resistance1-EXTRA_COLLISION_THRESHOLD) / EXTRA_COLLISION_THRESHOLD );
				}
			}

			collisions++;

			//Wires (since they don't have Integrity)
			ApplyWireDamage( i.Matrix1, cellPos1 );
			ApplyWireDamage( i.Matrix2, cellPos2 );

		}

		if ( collisions > 0 )
		{

			var epicenter = collisionLocations[collisionLocations.Count / 2];

			ExplosionUtils.PlaySoundAndShake(
				epicenter,
				(byte) Mathf.Clamp(collisions*12, 16, byte.MaxValue),
				Mathf.Clamp(collisions*8, 15, 127)
				);

			if ( collisions > 6 && Mathf.Max( i.Matrix1.Speed, i.Matrix2.Speed ) > 6 )
			{
				i.Matrix1.Matrix.OnEarthquake.Invoke( epicenter, collisions );
				i.Matrix2.Matrix.OnEarthquake.Invoke( epicenter, collisions );
			}
			collisionLocations.Clear();
		}

		//Damage methods
		void ApplyDamage( MatrixInfo victimMatrix, Vector3Int cellPos, float hitEnergy, Vector3Int worldPos )
		{
			//LivingHealthBehaviour
			hitEnergy -= ApplyLivingDamage( victimMatrix, cellPos, hitEnergy );

			//TilemapDamage
			ApplyTilemapDamage( victimMatrix, cellPos, hitEnergy, worldPos );

			//Integrity
			ApplyIntegrityDamage( victimMatrix, cellPos, hitEnergy );
		}

		void ApplyCritDamage( MatrixInfo victimMatrix, Vector3Int cellPos, Vector3Int worldPos )
		{
			//LivingHealthBehaviour
			ApplyLivingDamage( victimMatrix, cellPos, 9001 );

			//TilemapDamage
			ApplyTilemapDamage( victimMatrix, cellPos, 9001, worldPos);

			//Integrity
			ApplyIntegrityDamage( victimMatrix, cellPos, 9001 );
		}

		void ApplyTilemapDamage( MatrixInfo matrix, Vector3Int cellPos, float damage, Vector3Int worldPos )
		{
			if (matrix == null) return;

			matrix.MetaTileMap.ApplyDamage( cellPos, damage, worldPos );
			if ( damage > 9000 )
			{
				foreach ( var damageableLayer in matrix.MetaTileMap.LayersValues )
				{
					if (damageableLayer.LayerType == LayerType.Objects) continue;
					matrix.TileChangeManager.MetaTileMap.RemoveTileWithlayer( cellPos, damageableLayer.LayerType, false, removeAllMulti: true);
				}
			}
		}

		void ApplyWireDamage( MatrixInfo matrix, Vector3Int cellPos )
		{
			if (matrix == null) return;

			foreach ( var wire in matrix.Matrix.Get<CableInheritance>( cellPos, true ) )
			{
				if (Random.value >= 0.5)
				{
					// Sparks
					wire.QueueForDemolition(wire);
					StartCoroutine(DestroyWireWithDelay(wire, (byte)(Random.value * 20f)));
				}
				else
				{
					// Destruction
					wire.ToDestroy();
				}
			}
		}

		float ApplyIntegrityDamage( MatrixInfo matrix, Vector3Int cellPos, float damage )
		{
			if (matrix == null) return 0;

			float resistance = 0f;
			foreach ( var integrity in matrix.Matrix.Get<Integrity>( cellPos, true ) )
			{
				resistance += integrity.integrity;
				integrity.ApplyDamage( damage, AttackType.Melee, DamageType.Brute );
			}

			return resistance;
		}

		float ApplyLivingDamage( MatrixInfo matrix, Vector3Int cellPos, float damage )
		{
			if (matrix == null) return 0;

			byte count = 0;
			foreach ( var healthBehaviour in matrix.Matrix.Get<LivingHealthMasterBase>( cellPos, true ) )
			{
				healthBehaviour.ApplyDamageToBodyPart( matrix.GameObject, damage, AttackType.Melee, DamageType.Brute );
				count++;
			}

			return count * 50;
		}

		void TryPushing( MatrixInfo matrix, Vector3Int cellPos, Vector2Int pushVector, float speed )
		{
			if (matrix == null || pushVector == Vector2Int.zero) return;

			foreach ( var pushPull in matrix.Matrix.Get<UniversalObjectPhysics>( cellPos, true ) )
			{
				byte pushes = (byte) Mathf.Clamp( speed / 4, 1, 4 );
				for ( int j = 0; j < pushes; j++ )
				{
					pushPull.NewtonianPush( pushVector, speed * Random.Range( 0.8f, 1.1f ) );
				}
			}
		}

		IEnumerator DestroyWireWithDelay( CableInheritance wire, byte timer )
		{
			yield return WaitFor.Seconds( timer );
			if ( wire != null )
			{
				wire.ToDestroy();
			}
		}
	}

	private void OnDrawGizmos()
	{
		if (!Application.isPlaying || !IsInitialized) return;
		foreach ( var intersection in Instance.TrackedIntersections )
		{
			Gizmos.color = Color.red;
			DebugGizmoUtils.DrawRect( intersection.Matrix1.WorldBounds.Minimum, intersection.Matrix1.WorldBounds.Maximum );
			Gizmos.color = Color.blue;
			DebugGizmoUtils.DrawRect( intersection.Matrix2.WorldBounds.Minimum,  intersection.Matrix2.WorldBounds.Maximum);
			Gizmos.color = Color.yellow;
			DebugGizmoUtils.DrawRect( intersection.Rect.Minimum, intersection.Rect.Maximum );
		}
	}
}

/// <summary>
/// First and second matrix are swappable – intersections (m1,m2) and (m2,m1) will be considered equal.
/// Rect isn't checked for equality
/// </summary>
public readonly struct MatrixIntersection
{
	public readonly MatrixInfo Matrix1;
	public readonly MatrixInfo Matrix2;
	public readonly BetterBounds Rect;

	public MatrixIntersection(MatrixInfo matrix1, MatrixInfo matrix2, BetterBounds rect)
	{
		Matrix1 = matrix1;
		Matrix2 = matrix2;
		Rect = rect;
	}

	public MatrixIntersection Clone() => new MatrixIntersection(Matrix1, Matrix2, Rect);

	public MatrixIntersection Clone(ref BetterBounds rect) => new MatrixIntersection(Matrix1, Matrix2, rect);

	public override int GetHashCode()
	{
		return Matrix1.GetHashCode() ^ Matrix2.GetHashCode();
	}

	public bool Equals(MatrixIntersection other) =>
		Matrix1 == other.Matrix1 && Matrix2 == other.Matrix2
		|| Matrix1 == other.Matrix2 && Matrix2 == other.Matrix1;

	public override bool Equals( object obj )
	{
		return obj is MatrixIntersection other && Equals( other );
	}

	public static bool operator ==( MatrixIntersection left, MatrixIntersection right )
	{
		return left.Equals( right );
	}

	public static bool operator !=( MatrixIntersection left, MatrixIntersection right )
	{
		return !left.Equals( right );
	}
}
