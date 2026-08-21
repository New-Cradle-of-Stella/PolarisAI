using m2d;
using Polaris.API;
using UnityEngine;

namespace Polaris.AI;

internal sealed class CustomNpcProvider : INpcBodyProvider
{
    public bool CanSpawn(string definitionId) => definitionId == "custom.basic";

    public NpcSpawnResult? Spawn(NpcSpawnRequest request, GameVector2 position)
    {
        Map2d? map = M2DBase.Instance?.curMap;
        if (map == null) return null;
        string nativeKey = request.InstanceKey ?? $"polaris_npc_{position.X:0.##}_{position.Y:0.##}";
        PolarisNpcBody body = map.createMover<PolarisNpcBody>(nativeKey, position.X, position.Y);
        body.appear(map);
        map.assignMover(body);
        GameCharacter character = GameCharacter.Wrap(body);
        return new NpcSpawnResult(character, body.destruct, visible => SetVisible(body, visible), defaultFaction: "neutral");
    }

    static void SetVisible(M2Mover mover, bool visible)
    {
        foreach (Renderer renderer in mover.gameObject.GetComponentsInChildren<Renderer>(true)) renderer.enabled = visible;
    }
}

internal sealed class PolarisNpcBody : M2AttackableP
{
    static Sprite? markerSprite;

    public override bool isDamagingOrKo() => !is_alive;
    public override HITTYPE getHitType(M2Ray ray) => HITTYPE.NONE;

    public override void appear(Map2d map)
    {
        maxhp = 100;
        maxmp = 100;
        sizex = 0.25f;
        sizey = 0.5f;
        base.appear(map);
        floating = false;
        carryable_other_object = false;
        var renderer = gameObject.GetComponent<SpriteRenderer>() ?? gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = markerSprite ??= CreateMarkerSprite();
        renderer.color = new Color(0.2f, 0.85f, 1f, 0.9f);
    }

    static Sprite CreateMarkerSprite()
    {
        var texture = new Texture2D(32, 64, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            name = "PolarisAI.CustomNpcMarker",
        };
        var pixels = new Color[32 * 64];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, 32, 64), new Vector2(0.5f, 1f), 64f);
    }
}
