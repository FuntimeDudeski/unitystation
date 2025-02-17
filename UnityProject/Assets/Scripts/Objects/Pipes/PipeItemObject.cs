using System.Collections.Generic;
using UnityEngine;
using Systems.Pipes;
using Objects.Atmospherics;


namespace Items.Atmospherics
{
	public class PipeItemObject : PipeItem
	{
		public MonoPipe pipeObject;

		public override void BuildPipe()
		{
			var pipe = GetPipeObject();
			if (pipe == null) return;

			var spawn = Spawn.ServerPrefab(pipe.gameObject, registerItem.WorldPositionServer, localRotation: this.rotatable.ByDegreesToQuaternion(this.rotatable.CurrentDirection, Quaternion.identity));

			var monoPipe = spawn.GameObject.GetComponent<MonoPipe>();





			monoPipe.SetColour(Colour);
			monoPipe.directional.FaceDirection(this.rotatable.CurrentDirection);
			monoPipe.SetUpPipes();


			_ = Despawn.ServerSingle(gameObject);
		}

		public virtual void Setsprite()
		{
		}

		public virtual MonoPipe GetPipeObject()
		{
			return pipeObject;
		}

		public override Connections GetConnections()
		{
			if (pipeObject != null)
			{
				return pipeObject.pipeData.RotatedConnections.Copy();
			}

			return null;
		}
	}
}