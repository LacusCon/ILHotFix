using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D;

public class AtlasImage : Image
{
    [SerializeField] SpriteAtlas m_Atlas;
    public SpriteAtlas atlas{ get{ return m_Atlas; } set{ m_Atlas = value;}}
    [SerializeField] string m_SpriteName;
    public string spriteName{
        get{return m_SpriteName; }
        set {
            m_SpriteName = value; 

            if (atlas != null) {
                this.sprite = atlas.GetSprite (m_SpriteName);
            }
        }
    }

    protected override void OnEnable(){
        base.OnEnable ();
        if (atlas != null)
            this.sprite = atlas.GetSprite (spriteName);
    }
}