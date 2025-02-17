using System.Collections;
using Logs;
using UnityEngine;

namespace Core.Lighting
{
	public class HighlightScan : MonoBehaviour
	{
		private SpriteRenderer spriteRenderer;
		[SerializeField] private GameObject noHighlightSpriteObj;

		private void Awake()
		{
			if (CustomNetworkManager.IsHeadless)
			{
				Loggy.Error("Highlight scanner detected on headless server! Despawning self..");
				Despawn.ClientSingle(this.gameObject);
				return;
			}

			spriteRenderer = GetComponentInChildren<SpriteRenderer>();
			Color alphaZero = spriteRenderer.color;
			alphaZero.a = 0;
			spriteRenderer.color = alphaZero;

			HighlightScanManager.Instance.HighlightScans.Add(this);
		}

		private void OnEnable()
		{
			noHighlightSpriteObj ??= CustomNetworkManager.Instance.GetSpawnablePrefabFromName("_RandomItemSpawnerBase");
		}

		private void OnDisable()
		{
			HighlightScanManager.Instance.OrNull()?.HighlightScans.Remove(this);
		}

		public void Setup(Sprite sprite, bool disableAlphaChangeOnAwake = false)
		{
			if (sprite == null)
			{
				spriteRenderer.sprite = noHighlightSpriteObj.GetComponentInChildren<SpriteRenderer>()?.sprite;
				return;
			}
			spriteRenderer.sprite = sprite;
			if (disableAlphaChangeOnAwake)
			{
				Color alpha = spriteRenderer.color;
				alpha.a = 0.65f;
				spriteRenderer.color = alpha;
				gameObject.layer = 10;
				spriteRenderer.gameObject.layer = 10;
			}
		}

		public IEnumerator Highlight()
		{
			Color noAlpha = spriteRenderer.color;
			Color alpha = spriteRenderer.color;
			alpha.a = 0.99f;
			spriteRenderer.color = alpha;
			while (spriteRenderer.color.a > 0.1f)
			{
				yield return WaitFor.EndOfFrame;
				alpha = Color.Lerp(alpha, noAlpha, 0.5f * Time.deltaTime);
				spriteRenderer.color = alpha;
			}

			alpha.a = 0;
			spriteRenderer.color = alpha;
		}
	}
}