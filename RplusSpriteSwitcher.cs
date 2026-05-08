using System.Collections.Generic;

using UnityEngine;

public class RplusSpriteSwitcher : MonoBehaviour {
	private List<SpriteCapture> _CapturedSprite;

	public void ActivateRplus(bool rPlus) {
		if (!rPlus) {
			foreach (var sp in this._CapturedSprite)
				sp._rederer.sprite = sp._spriteOrigin;
		}
		else {
			foreach (var sp in this._CapturedSprite)
				sp._rederer.sprite = sp._spriteRplus;
		}
	}

	public RplusSpriteSwitcher() {
		this._CapturedSprite = new List<SpriteCapture>();
	}

	public void Start() {
		this.ActivateRplus(true);
	}

	public class SpriteCapture {
		public SpriteRenderer _rederer;
		public Sprite _spriteOrigin;
		public Sprite _spriteRplus;
	}
}
