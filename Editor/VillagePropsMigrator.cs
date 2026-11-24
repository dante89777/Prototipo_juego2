using System;
using System.Collections.Generic;
using System.IO;
using Platformer.Core;
using Platformer.Mechanics;
using Platformer.Model;
using Platformer.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

/// <summary>
/// Genera el nivel definitivo del prototipo: terreno continuo con escalones
/// alcanzables, cofres que otorgan elementos, pared elemental, zonas de
/// peligro, enemigos, jefe, monedas y HUD.
///
/// Puede ejecutarse desde el menú "Village/..." o de forma automática creando
/// el archivo centinela "_migrate_village.flag" en la raíz del proyecto.
/// </summary>
[InitializeOnLoad]
public static class VillagePropsMigrator
{
    const string PrefabBase = "Assets/Cainos/Pixel Art Platformer - Village Props/Prefab/";
    const string TilePath = "Assets/Cainos/Pixel Art Platformer - Village Props/Tileset Palette/TP Ground/";
    const string AudioBase = "Assets/Audio/";
    const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    const string FlagName = "_migrate_village.flag";
    const string PropsRootName = "Village_Props";
    const string HudName = "HUD";
    const string TopTileSpritePath = "Assets/Environment/Sprites/TileGroundTop.png";
    const string TopTileAssetPath = "Assets/Terrain/Generated/Tile_GroundTop.asset";
    const string BaseTileAssetPath = "Assets/Terrain/Generated/Tile_Ground.asset";
    const string TopTilemapName = "TerrainTop";

    static readonly Color[] RegionColors =
    {
        new Color(0.62f, 0.74f, 0.92f),  // Crateria
        new Color(0.52f, 0.92f, 0.55f),  // Brinstar
        new Color(1.00f, 0.48f, 0.34f),  // Norfair
        new Color(0.44f, 0.80f, 1.00f),  // Maridia
        new Color(0.82f, 0.86f, 0.96f),  // Wrecked Ship
        new Color(0.80f, 0.44f, 1.00f),  // Tourian
    };

    const float JumpModifier = 1f;
    const float JumpDeceleration = 0.85f;
    const float PlayerMaxSpeed = 4f;
    const float PlayerJumpTakeOffSpeed = 8f;
    const float PlayerStartX = -28f;
    const float WallX = 56f;
    const float VictoryX = 60f;

    // Alturas desde las que se busca la superficie del terreno.
    const float StartLevel = 50f;       // Crateria, suelo y=48
    const float BrinstarHigh = 32f;     // Brinstar, suelo y=30
    const float BrinstarMain = 22f;     // Brinstar, suelo y=20
    const float BrinstarLow = 14f;      // Brinstar, suelo y=12
    const float MaridiaHigh = 30f;      // Maridia, suelo y=28
    const float MaridiaMain = 22f;      // Maridia, suelo y=20
    const float MaridiaLow = 14f;       // Maridia, suelo y=12
    const float NorfairLevel = 2f;      // Norfair, suelo y=0
    const float TopLevel = 46f;         // Wrecked Ship / Tourian, suelo y=44

    // Capas de orden de dibujado: props (3) < monedas (4) < jugador y enemigos (5).
    const int PropSortingOrder = 3;
    const int TokenSortingOrder = 4;

    struct Block
    {
        public float xMin, xMax, top, bottom;
        public Block(float xMin, float xMax, float top, float bottom)
        {
            this.xMin = xMin; this.xMax = xMax; this.top = top; this.bottom = bottom;
        }
    }

    // Terreno del nivel, inspirado en el mapa de Super Metroid.
    // La superficie real de un bloque es su borde superior ("top"): los tiles
    // se pintan con el centro dentro del bloque, asi que el ultimo tile deja
    // el borde justo en "top". Por eso los props se buscan con
    // SurfaceY(..., techo).
    static readonly Block[] Blocks =
    {
        // Limites del mundo
        new Block(-42f, -38f, 60f, -10f),
        new Block(-42f, 64f, 62f, 60f),
        new Block(62f, 64f, 60f, -10f),

        // Crateria: inicio arriba-izquierda, con hueco para el shaft de bajada.
        new Block(-38f, -16f, 48f, 46f),
        new Block(-10f, -6f, 48f, 46f),
        new Block(-18f, -16f, 48f, 28f),
        new Block(-10f, -8f, 48f, 28f),
        new Block(-16f, -12f, 44f, 42f),
        new Block(-14f, -10f, 40f, 38f),
        new Block(-16f, -12f, 36f, 34f),
        new Block(-14f, -10f, 32f, 30f),

        // Brinstar: centro-izquierda, con escalones de 2 u entre niveles.
        new Block(-38f, 12f, 30f, 28f),
        new Block(-38f, 8f, 20f, 18f),
        new Block(8f, 16f, 20f, 18f),
        new Block(-38f, 12f, 12f, 10f),
        new Block(12f, 16f, 12f, 10f),
        new Block(-36f, -32f, 14f, 12f),
        new Block(-32f, -28f, 16f, 14f),
        new Block(-28f, -24f, 18f, 16f),
        new Block(-32f, -28f, 22f, 20f),
        new Block(-28f, -24f, 24f, 22f),
        new Block(-24f, -20f, 26f, 24f),
        new Block(-20f, -16f, 28f, 26f),
        new Block(-16f, -12f, 22f, 20f),
        new Block(-12f, -8f, 24f, 22f),
        new Block(-8f, -4f, 26f, 24f),
        new Block(-4f, 0f, 28f, 26f),
        new Block(0f, 4f, 22f, 20f),
        new Block(4f, 8f, 24f, 22f),
        new Block(8f, 12f, 26f, 24f),

        // Maridia: centro-derecha, con puentes y rampas de 2 u.
        new Block(16f, 50f, 28f, 26f),
        new Block(54f, 58f, 28f, 26f),
        new Block(16f, 50f, 20f, 18f),
        new Block(54f, 58f, 20f, 18f),
        new Block(16f, 50f, 12f, 10f),
        new Block(54f, 56f, 12f, 10f),
        new Block(20f, 24f, 14f, 12f),
        new Block(24f, 28f, 16f, 14f),
        new Block(28f, 32f, 18f, 16f),
        new Block(32f, 36f, 20f, 18f),
        new Block(20f, 24f, 22f, 20f),
        new Block(24f, 28f, 24f, 22f),
        new Block(28f, 32f, 26f, 24f),
        new Block(36f, 40f, 22f, 20f),
        new Block(40f, 44f, 24f, 22f),
        new Block(44f, 48f, 26f, 24f),

        // Bajada a Norfair: escalera continua de 2 u.
        new Block(24f, 28f, 10f, 8f),
        new Block(24f, 28f, 8f, 6f),
        new Block(24f, 28f, 6f, 4f),
        new Block(24f, 28f, 4f, 2f),
        new Block(24f, 28f, 2f, 0f),

        // Norfair: abajo-derecha
        new Block(4f, 50f, 10f, 8f),
        new Block(54f, 60f, 10f, 8f),
        new Block(4f, 60f, 0f, -8f),
        new Block(8f, 12f, 2f, 0f),
        new Block(12f, 16f, 4f, 2f),
        new Block(16f, 20f, 6f, 4f),
        new Block(32f, 36f, 6f, 4f),
        new Block(36f, 40f, 4f, 2f),
        new Block(40f, 44f, 2f, 0f),

        // Shaft de subida derecha: peldaños alternos de 2 u.
        new Block(50f, 54f, 2f, 0f),
        new Block(46f, 50f, 4f, 2f),
        new Block(50f, 54f, 6f, 4f),
        new Block(46f, 50f, 8f, 6f),
        new Block(50f, 54f, 10f, 8f),
        new Block(46f, 50f, 12f, 10f),
        new Block(50f, 54f, 14f, 12f),
        new Block(46f, 50f, 16f, 14f),
        new Block(50f, 54f, 18f, 16f),
        new Block(46f, 50f, 20f, 18f),
        new Block(50f, 54f, 22f, 20f),
        new Block(46f, 50f, 24f, 22f),
        new Block(50f, 54f, 26f, 24f),
        new Block(46f, 50f, 28f, 26f),
        new Block(50f, 54f, 30f, 28f),
        new Block(46f, 50f, 32f, 30f),
        new Block(50f, 54f, 34f, 32f),
        new Block(46f, 50f, 36f, 34f),
        new Block(50f, 54f, 38f, 36f),
        new Block(46f, 50f, 40f, 38f),
        new Block(50f, 54f, 42f, 40f),

        // Wrecked Ship y Tourian: el hueco central deja salir el shaft.
        new Block(16f, 46f, 44f, 42f),
        new Block(54f, 62f, 44f, 42f),
    };

    struct PropDef
    {
        public string prefab, objName;
        public float x, yOffset, ceiling;
        public PropDef(string prefab, float x, float yOffset, string objName, float ceiling)
        {
            this.prefab = prefab; this.x = x; this.yOffset = yOffset; this.objName = objName; this.ceiling = ceiling;
        }
    }

    static readonly PropDef[] Decor =
    {
        // Crateria: zona de inicio, arriba-izquierda.
        new PropDef("PF Village Props - Tree 01", -34f, 0f, "Arbol_Inicio", StartLevel),
        new PropDef("PF Village Props - Barrel", -30f, 0f, "Barril_Inicio", StartLevel),
        new PropDef("PF Village Props - Sign 01", -27f, 0f, "Senal_Inicio", StartLevel),
        new PropDef("PF Village Props - Grass 01", -24f, 0f, "Hierba_01", StartLevel),
        new PropDef("PF Village Props - Well", -20f, 0f, "Pozo_Inicio", StartLevel),
        new PropDef("PF Village Props - Torch", -8f, 1.4f, "Antorcha_01", StartLevel),
        new PropDef("PF Village Props - Flower 01", -7f, 0f, "Flor_01", StartLevel),

        // Shaft de bajada.
        new PropDef("PF Village Props - Torch", -14f, 1.4f, "Antorcha_Shaft_1", 45f),
        new PropDef("PF Village Props - Bush 01", -14f, 0f, "Arbusto_Shaft_2", 41f),
        new PropDef("PF Village Props - Torch", -14f, 1.4f, "Antorcha_Shaft_3", 37f),
        new PropDef("PF Village Props - Grass 02", -14f, 0f, "Hierba_Shaft_4", 33f),

        // Brinstar: estantes, suelo principal y suelo bajo.
        new PropDef("PF Village Props - Tree 02", -34f, 0f, "Arbol_Brinstar_Alto", BrinstarHigh),
        new PropDef("PF Village Props - Barrel", -26f, 0f, "Barril_Brinstar_Alto", BrinstarHigh),
        new PropDef("PF Village Props - Banner", -18f, 2f, "Estandarte_Brinstar", BrinstarHigh),
        new PropDef("PF Village Props - Grass 05", -8f, 0f, "Hierba_Brinstar_Alta", BrinstarHigh),
        new PropDef("PF Village Props - Torch", 4f, 1.4f, "Antorcha_Brinstar_Alta", BrinstarHigh),
        new PropDef("PF Village Props - Road Sign 01", 8f, 0f, "Senal_Brinstar", BrinstarHigh),
        new PropDef("PF Village Props - Campfire 01", -32f, 0f, "Fuego_Brinstar", BrinstarMain),
        new PropDef("PF Village Props - Bush 02", -24f, 0f, "Arbusto_Brinstar_1", BrinstarMain),
        new PropDef("PF Village Props - Statue", -16f, 0f, "Estatua_Brinstar", BrinstarMain),
        new PropDef("PF Village Props - Tree 01", -4f, 0f, "Arbol_Brinstar_Medio", BrinstarMain),
        new PropDef("PF Village Props - Hay Bale", 12f, 0f, "Fardo_Brinstar", BrinstarMain),
        new PropDef("PF Village Props - Flower 02", -34f, 0f, "Flor_Brinstar_Baja", BrinstarLow),
        new PropDef("PF Village Props - Fence A", -22f, 0f, "Valla_Brinstar", BrinstarLow),
        new PropDef("PF Village Props - Road Lamp", -6f, 2f, "Lampara_Brinstar", BrinstarLow),
        new PropDef("PF Village Props - Bush 01", 6f, 0f, "Arbusto_Brinstar_2", BrinstarLow),
        new PropDef("PF Village Props - Billboard", -30f, 2f, "Cartel_Brinstar", 23f),
        new PropDef("PF Village Props - Torch", -22f, 1.4f, "Antorcha_Parkour_Brinstar", 25f),

        // Maridia: tres niveles conectados por rampas.
        new PropDef("PF Village Props - Tree 01", 20f, 0f, "Arbol_Maridia_Alto", MaridiaHigh),
        new PropDef("PF Village Props - Banner", 32f, 2f, "Estandarte_Maridia", MaridiaHigh),
        new PropDef("PF Village Props - Grass 03", 44f, 0f, "Hierba_Maridia_Alta", MaridiaHigh),
        new PropDef("PF Village Props - Torch", 48f, 1.4f, "Antorcha_Maridia_Alta", MaridiaHigh),
        new PropDef("PF Village Props - Barrel", 18f, 0f, "Barril_Maridia", MaridiaMain),
        new PropDef("PF Village Props - Statue", 30f, 0f, "Estatua_Maridia", MaridiaMain),
        new PropDef("PF Village Props - Tree 02", 42f, 0f, "Arbol_Maridia_Medio", MaridiaMain),
        new PropDef("PF Village Props - Fence A", 56f, 0f, "Valla_Maridia", MaridiaMain),
        new PropDef("PF Village Props - Bush 02", 22f, 0f, "Arbusto_Maridia_Bajo", MaridiaLow),
        new PropDef("PF Village Props - Road Lamp", 38f, 2f, "Lampara_Maridia", MaridiaLow),
        new PropDef("PF Village Props - Flower 01", 48f, 0f, "Flor_Maridia", MaridiaLow),
        new PropDef("PF Village Props - Campfire 01", 55f, 0f, "Fuego_Maridia", MaridiaLow),

        // Norfair: suelo inferior, abajo-derecha.
        new PropDef("PF Village Props - Torch", 6f, 1.4f, "Antorcha_Norfair_1", NorfairLevel),
        new PropDef("PF Village Props - Bush 01", 14f, 0f, "Arbusto_Norfair", NorfairLevel),
        new PropDef("PF Village Props - Statue", 26f, 0f, "Estatua_Norfair", NorfairLevel),
        new PropDef("PF Village Props - Campfire 01", 38f, 0f, "Fuego_Norfair", NorfairLevel),
        new PropDef("PF Village Props - Torch", 50f, 1.4f, "Antorcha_Norfair_2", NorfairLevel),
        new PropDef("PF Village Props - Hay Bale", 58f, 0f, "Fardo_Norfair", NorfairLevel),

        // Shaft de subida y Wrecked Ship / Tourian.
        new PropDef("PF Village Props - Torch", 52f, 1.4f, "Antorcha_Shaft_Derecho_1", 5f),
        new PropDef("PF Village Props - Torch", 52f, 1.4f, "Antorcha_Shaft_Derecho_2", 21f),
        new PropDef("PF Village Props - Torch", 52f, 1.4f, "Antorcha_Shaft_Derecho_3", 37f),
        new PropDef("PF Village Props - Tree 02", 20f, 0f, "Arbol_Wrecked", TopLevel),
        new PropDef("PF Village Props - Banner", 32f, 2f, "Estandarte_Wrecked", TopLevel),
        new PropDef("PF Village Props - Billboard", 44f, 2f, "Cartel_Tourian", TopLevel),
        new PropDef("PF Village Props - Torch", 54f, 1.4f, "Antorcha_Tourian", TopLevel),
        new PropDef("PF Village Props - Tree 01", 60f, 0f, "Arbol_Final", TopLevel),
    };

    struct TokenSpot
    {
        public float x, ceiling, height;
        public TokenSpot(float x, float ceiling, float height)
        {
            this.x = x; this.ceiling = ceiling; this.height = height;
        }
    }

    // Monedas: x, techo de busqueda y altura sobre la superficie.
    static readonly TokenSpot[] TokenSpots =
    {
        // Crateria y shaft de bajada.
        new TokenSpot(-36f, StartLevel, 0.7f), new TokenSpot(-32f, StartLevel, 0.7f),
        new TokenSpot(-28f, StartLevel, 0.7f), new TokenSpot(-24f, StartLevel, 0.7f),
        new TokenSpot(-21f, StartLevel, 0.7f), new TokenSpot(-19f, StartLevel, 0.7f),
        new TokenSpot(-9f, StartLevel, 0.7f), new TokenSpot(-7f, StartLevel, 0.7f),
        new TokenSpot(-14f, 45f, 0.7f), new TokenSpot(-14f, 41f, 0.7f),
        new TokenSpot(-14f, 37f, 0.7f), new TokenSpot(-14f, 33f, 0.7f),

        // Brinstar: tres niveles y plataformas de parkour.
        new TokenSpot(-36f, BrinstarHigh, 0.7f), new TokenSpot(-32f, BrinstarHigh, 0.7f),
        new TokenSpot(-28f, BrinstarHigh, 0.7f), new TokenSpot(-24f, BrinstarHigh, 0.7f),
        new TokenSpot(-20f, BrinstarHigh, 0.7f), new TokenSpot(-16f, BrinstarHigh, 0.7f),
        new TokenSpot(-12f, BrinstarHigh, 0.7f), new TokenSpot(-8f, BrinstarHigh, 0.7f),
        new TokenSpot(-4f, BrinstarHigh, 0.7f), new TokenSpot(0f, BrinstarHigh, 0.7f),
        new TokenSpot(-36f, BrinstarMain, 0.7f), new TokenSpot(-32f, BrinstarMain, 0.7f),
        new TokenSpot(-28f, BrinstarMain, 0.7f), new TokenSpot(-24f, BrinstarMain, 0.7f),
        new TokenSpot(-20f, BrinstarMain, 0.7f), new TokenSpot(-16f, BrinstarMain, 0.7f),
        new TokenSpot(-12f, BrinstarMain, 0.7f), new TokenSpot(-8f, BrinstarMain, 0.7f),
        new TokenSpot(-4f, BrinstarMain, 0.7f), new TokenSpot(0f, BrinstarMain, 0.7f),
        new TokenSpot(8f, BrinstarMain, 0.7f), new TokenSpot(14f, BrinstarMain, 0.7f),
        new TokenSpot(-36f, BrinstarLow, 0.7f), new TokenSpot(-32f, BrinstarLow, 0.7f),
        new TokenSpot(-28f, BrinstarLow, 0.7f), new TokenSpot(-24f, BrinstarLow, 0.7f),
        new TokenSpot(-20f, BrinstarLow, 0.7f), new TokenSpot(-16f, BrinstarLow, 0.7f),
        new TokenSpot(-12f, BrinstarLow, 0.7f), new TokenSpot(-8f, BrinstarLow, 0.7f),
        new TokenSpot(-4f, BrinstarLow, 0.7f), new TokenSpot(0f, BrinstarLow, 0.7f),
        new TokenSpot(-30f, 23f, 0.7f), new TokenSpot(-22f, 25f, 0.7f),
        new TokenSpot(-10f, 27f, 0.7f), new TokenSpot(2f, 29f, 0.7f),

        // Maridia: tres niveles.
        new TokenSpot(18f, MaridiaHigh, 0.7f), new TokenSpot(24f, MaridiaHigh, 0.7f),
        new TokenSpot(30f, MaridiaHigh, 0.7f), new TokenSpot(36f, MaridiaHigh, 0.7f),
        new TokenSpot(42f, MaridiaHigh, 0.7f), new TokenSpot(44f, MaridiaHigh, 0.7f),
        new TokenSpot(46f, MaridiaHigh, 0.7f), new TokenSpot(48f, MaridiaHigh, 0.7f),
        new TokenSpot(18f, MaridiaMain, 0.7f), new TokenSpot(24f, MaridiaMain, 0.7f),
        new TokenSpot(30f, MaridiaMain, 0.7f), new TokenSpot(36f, MaridiaMain, 0.7f),
        new TokenSpot(42f, MaridiaMain, 0.7f), new TokenSpot(46f, MaridiaMain, 0.7f),
        new TokenSpot(48f, MaridiaMain, 0.7f), new TokenSpot(56f, MaridiaMain, 0.7f),
        new TokenSpot(18f, MaridiaLow, 0.7f), new TokenSpot(26f, MaridiaLow, 0.7f),
        new TokenSpot(34f, MaridiaLow, 0.7f), new TokenSpot(42f, MaridiaLow, 0.7f),
        new TokenSpot(48f, MaridiaLow, 0.7f), new TokenSpot(55f, MaridiaLow, 0.7f),

        // Norfair, shaft de subida y suelo final.
        new TokenSpot(6f, NorfairLevel, 0.7f), new TokenSpot(12f, NorfairLevel, 0.7f),
        new TokenSpot(18f, NorfairLevel, 0.7f), new TokenSpot(24f, NorfairLevel, 0.7f),
        new TokenSpot(30f, NorfairLevel, 0.7f), new TokenSpot(36f, NorfairLevel, 0.7f),
        new TokenSpot(42f, NorfairLevel, 0.7f), new TokenSpot(48f, NorfairLevel, 0.7f),
        new TokenSpot(54f, NorfairLevel, 0.7f), new TokenSpot(58f, NorfairLevel, 0.7f),
        new TokenSpot(52f, 5f, 0.7f), new TokenSpot(52f, 13f, 0.7f),
        new TokenSpot(52f, 21f, 0.7f), new TokenSpot(52f, 29f, 0.7f),
        new TokenSpot(52f, 37f, 0.7f), new TokenSpot(52f, 45f, 0.7f),
        new TokenSpot(20f, TopLevel, 0.7f), new TokenSpot(30f, TopLevel, 0.7f),
        new TokenSpot(40f, TopLevel, 0.7f), new TokenSpot(48f, TopLevel, 0.7f),
        new TokenSpot(54f, TopLevel, 0.7f), new TokenSpot(60f, TopLevel, 0.7f),
    };

    static double nextFlagCheck;

    static VillagePropsMigrator()
    {
        EditorApplication.update += WatchFlag;
    }

    static void WatchFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.timeSinceStartup < nextFlagCheck) return;
        nextFlagCheck = EditorApplication.timeSinceStartup + 0.5;

        string flag = FlagPath();
        if (!File.Exists(flag)) return;

        if (Migrate())
        {
            File.Delete(flag);
            Debug.Log("[VillagePropsMigrator] Migracion automatica completada.");
        }
        else
        {
            Debug.LogWarning("[VillagePropsMigrator] Migracion automatica pospuesta: revisa que SampleScene este abierta.");
        }
    }

    static string FlagPath()
    {
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, FlagName);
    }

    [MenuItem("Village/Migrar nivel definitivo")]
    public static void MigrateFromMenu()
    {
        Migrate();
    }

    public static void BatchMigrate()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        if (!Migrate())
            throw new Exception("[VillagePropsMigrator] BatchMigrate fallo.");
    }

    public static bool Migrate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[VillagePropsMigrator] Sal del modo play antes de migrar.");
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogError("[VillagePropsMigrator] No hay escena activa.");
            return false;
        }

        Tilemap level = FindCollisionTilemap();
        if (level == null)
        {
            Debug.LogError("[VillagePropsMigrator] No se encontro tilemap con TilemapCollider2D.");
            return false;
        }

        if (EditorSettings.enterPlayModeOptionsEnabled)
            EditorSettings.enterPlayModeOptionsEnabled = false;

        int tiles = BuildTerrain(level);
        HideBackgrounds();

        Transform propsRoot = CreatePropsRoot();
        Tilemap map = level;
        PlaceDecor(map, propsRoot);
        PlaceChests(map, propsRoot);
        PlaceBraziers(map, propsRoot);
        PlaceElementalWall(map, propsRoot);

        SetupEntities(map);
        DisableLeftoverProps(propsRoot);
        SetupHud();
        ConfigureModel();
        UpdateConfiner();
        RemoveSceneObjectGuids();
        RecordAllInstances();

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        if (saved)
            Debug.Log($"[VillagePropsMigrator] Nivel definitivo guardado ({tiles} tiles) en {scene.path}");
        else
            Debug.LogError("[VillagePropsMigrator] No se pudo guardar la escena.");

        return saved;
    }

    static Tilemap FindCollisionTilemap()
    {
        foreach (Tilemap tm in UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude))
        {
            if (tm.GetComponent<TilemapCollider2D>() != null)
                return tm;
        }
        return null;
    }

    static int BuildTerrain(Tilemap level)
    {
        TileBase tile = LoadOrCreateBaseTile();
        if (tile == null)
        {
            Debug.LogError("[VillagePropsMigrator] No se encontro el tile del tileset.");
            return 0;
        }

        TileBase topTile = LoadOrCreateTopTile();
        Tilemap top = FindOrCreateTopTilemap(level);
        top.ClearAllTiles();

        level.ClearAllTiles();

        TilemapRenderer rend = level.GetComponent<TilemapRenderer>();
        if (rend != null)
        {
            rend.enabled = true;
            rend.sortingOrder = 0;
        }

        int count = 0;
        HashSet<Vector3Int> painted = new HashSet<Vector3Int>();

        foreach (Block b in Blocks)
        {
            Vector3Int min = level.WorldToCell(new Vector3(b.xMin, b.bottom, 0f));
            Vector3Int max = level.WorldToCell(new Vector3(b.xMax, b.top, 0f));
            int topY = level.WorldToCell(new Vector3(0f, b.top - 0.01f, 0f)).y;

            for (int x = min.x; x <= max.x; x++)
            {
                for (int y = min.y; y <= max.y; y++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    Vector3 center = level.GetCellCenterWorld(cell);

                    if (center.x < b.xMin || center.x >= b.xMax) continue;
                    if (center.y < b.bottom || center.y >= b.top) continue;
                    if (painted.Contains(cell)) continue;

                    level.SetTile(cell, tile);
                    Color region = RegionColors[RegionIndex(center)];
                    level.SetColor(cell, region);
                    painted.Add(cell);
                    count++;

                    if (topTile != null && y == topY)
                    {
                        top.SetTile(cell, topTile);
                        top.SetColor(cell, Color.Lerp(region, Color.white, 0.4f));
                    }
                }
            }
        }

        level.RefreshAllTiles();
        top.RefreshAllTiles();
        return count;
    }

    static Tilemap FindOrCreateTopTilemap(Tilemap level)
    {
        foreach (Tilemap tm in UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include))
        {
            if (tm.name == TopTilemapName) return tm;
        }

        GameObject go = new GameObject(TopTilemapName, typeof(Tilemap), typeof(TilemapRenderer));
        Grid grid = level.GetComponentInParent<Grid>();
        if (grid != null) go.transform.SetParent(grid.transform, false);

        TilemapRenderer renderer = go.GetComponent<TilemapRenderer>();
        renderer.sortingOrder = 1;
        return go.GetComponent<Tilemap>();
    }

    static TileBase LoadOrCreateTopTile()
    {
        TileBase existing = AssetDatabase.LoadAssetAtPath<TileBase>(TopTileAssetPath);
        if (existing != null) return existing;

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TopTileSpritePath);
        if (sprite == null)
        {
            Debug.LogWarning("[VillagePropsMigrator] No se encontro " + TopTileSpritePath);
            return null;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Terrain")) AssetDatabase.CreateFolder("Assets", "Terrain");
        if (!AssetDatabase.IsValidFolder("Assets/Terrain/Generated")) AssetDatabase.CreateFolder("Assets/Terrain", "Generated");

        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.colliderType = Tile.ColliderType.None;
        AssetDatabase.CreateAsset(tile, TopTileAssetPath);
        AssetDatabase.SaveAssets();
        return tile;
    }

    static TileBase LoadOrCreateBaseTile()
    {
        TileBase existing = AssetDatabase.LoadAssetAtPath<TileBase>(BaseTileAssetPath);
        if (existing != null) return existing;

        Tile original = AssetDatabase.LoadAssetAtPath<Tile>(TilePath + "TX Tileset Ground_0.asset");
        if (original == null) return null;

        if (!AssetDatabase.IsValidFolder("Assets/Terrain")) AssetDatabase.CreateFolder("Assets", "Terrain");
        if (!AssetDatabase.IsValidFolder("Assets/Terrain/Generated")) AssetDatabase.CreateFolder("Assets/Terrain", "Generated");

        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = original.sprite;
        tile.color = Color.white;
        tile.colliderType = original.colliderType;
        tile.flags = TileFlags.None;
        AssetDatabase.CreateAsset(tile, BaseTileAssetPath);
        AssetDatabase.SaveAssets();
        return tile;
    }

    static int RegionIndex(Vector3 world)
    {
        if (world.y >= 42f && world.x >= 36f) return 5;              // Tourian
        if (world.y >= 42f && world.x >= 16f) return 4;              // Wrecked Ship
        if (world.y < 10f) return 2;                                 // Norfair
        if (world.x >= 16f && world.y >= 10f && world.y < 30f) return 3; // Maridia
        if (world.x < 16f && world.y >= 10f && world.y < 32f) return 1;   // Brinstar
        return 0;                                                    // Crateria
    }

    static void HideBackgrounds()
    {
        string[] names = { "Background", "FarBackground", "FarBackground " };
        foreach (string name in names)
        {
            GameObject go = GameObject.Find(name);
            if (go == null) continue;

            foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>(true))
                sr.enabled = false;
            foreach (TilemapRenderer tr in go.GetComponentsInChildren<TilemapRenderer>(true))
                tr.enabled = false;
        }
    }

    static Transform CreatePropsRoot()
    {
        GameObject old = GameObject.Find(PropsRootName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);

        GameObject root = new GameObject(PropsRootName);
        return root.transform;
    }

    /// <summary>
    /// Oculta los props de la demo original de la plantilla que quedaban
    /// flotando en el vacio: el nivel de ejemplo del asset esta muy por encima
    /// de este mapa y sus tiles fueron borrados al rehacer el terreno.
    /// </summary>
    static void DisableLeftoverProps(Transform propsRoot)
    {
        List<Transform> keep = new List<Transform>();

        void Add(GameObject go)
        {
            if (go != null) keep.Add(go.transform);
        }

        foreach (PlayerController c in UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include)) Add(c.gameObject);
        foreach (EnemyController c in UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include)) Add(c.gameObject);
        foreach (TokenInstance c in UnityEngine.Object.FindObjectsByType<TokenInstance>(FindObjectsInactive.Include)) Add(c.gameObject);
        foreach (SpawnPoint c in UnityEngine.Object.FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include)) Add(c.gameObject);
        foreach (VictoryZone c in UnityEngine.Object.FindObjectsByType<VictoryZone>(FindObjectsInactive.Include)) Add(c.gameObject);
        foreach (DeathZone c in UnityEngine.Object.FindObjectsByType<DeathZone>(FindObjectsInactive.Include)) Add(c.gameObject);
        foreach (Tilemap c in UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include)) Add(c.gameObject);
        foreach (Camera c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include)) Add(c.gameObject);
        Add(GameObject.Find("CinemachineConfiner"));
        Add(GameObject.Find(HudName));

        List<string> disabled = new List<string>();

        foreach (GameObject go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
        {
            if (go == null || !go.activeSelf) continue;
            if (go.transform.IsChildOf(propsRoot)) continue;

            bool kept = false;
            foreach (Transform k in keep)
            {
                if (go.transform == k || go.transform.IsChildOf(k) || k.IsChildOf(go.transform))
                {
                    kept = true;
                    break;
                }
            }
            if (kept) continue;

            if (go.GetComponentInChildren<SpriteRenderer>() == null) continue;

            go.SetActive(false);
            disabled.Add(go.name);
        }

        if (disabled.Count > 0)
            Debug.Log("[VillagePropsMigrator] Props de la demo ocultados: " + disabled.Count + " -> " + string.Join(", ", disabled.ToArray()));
    }

    /// <summary>
    /// Superficie del terreno en la columna x. Se busca el primer suelo por
    /// debajo de ceilingY, de modo que las columnas con varios niveles
    /// (estante, suelo superior) devuelven la altura correcta.
    /// </summary>
    static float SurfaceY(Tilemap level, float worldX, float ceilingY)
    {
        Vector3Int column = level.WorldToCell(new Vector3(worldX, ceilingY, 0f));

        for (int y = column.y; y > column.y - 200; y--)
        {
            Vector3Int cell = new Vector3Int(column.x, y, 0);
            if (level.GetTile(cell) == null) continue;
            return level.GetCellCenterWorld(cell).y + level.cellSize.y * 0.5f;
        }

        return 0f;
    }

    static void PlaceDecor(Tilemap level, Transform root)
    {
        foreach (PropDef def in Decor)
        {
            float y = SurfaceY(level, def.x, def.ceiling) + def.yOffset;
            Spawn(def.prefab, new Vector3(def.x, y, 0f), root, def.objName);
        }
    }

    static void PlaceChests(Tilemap level, Transform root)
    {
        AudioClip open = LoadAudio("Collectable");

        GameObject fire = Spawn("PF Village Props - Chest Golden", new Vector3(-22f, SurfaceY(level, -22f, BrinstarHigh), 0f), root, "Cofre_Elemento_Fuego");
        SetupChest(fire, GameElement.Fuego, true, false, new Color(1f, 0.55f, 0.15f), open);

        GameObject grapple = Spawn("PF Village Props - Chest Wooden", new Vector3(2f, SurfaceY(level, 2f, BrinstarMain), 0f), root, "Cofre_Gancho");
        SetupChest(grapple, GameElement.Ninguno, false, true, new Color(0.75f, 0.6f, 0.35f), open);

        GameObject rayo = Spawn("PF Village Props - Chest Iron", new Vector3(44f, SurfaceY(level, 44f, MaridiaHigh), 0f), root, "Cofre_Elemento_Rayo");
        SetupChest(rayo, GameElement.Rayo, true, false, new Color(1f, 0.95f, 0.25f), open);
    }

    static void SetupChest(GameObject chest, GameElement element, bool grantsAttack, bool grantsGrapple, Color glow, AudioClip open)
    {
        if (chest == null) return;

        BoxCollider2D collider = chest.GetComponent<BoxCollider2D>();
        if (collider == null) collider = chest.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(1.4f, 1.6f);
        collider.offset = new Vector2(0f, 0.5f);

        ChestPickup pickup = chest.GetComponent<ChestPickup>();
        if (pickup == null) pickup = chest.AddComponent<ChestPickup>();

        SetProperty(pickup, "element", p => p.enumValueIndex = (int)element);
        SetProperty(pickup, "grantsAttack", p => p.boolValue = grantsAttack);
        SetProperty(pickup, "grantsGrapple", p => p.boolValue = grantsGrapple);
        SetProperty(pickup, "glowColor", p => p.colorValue = glow);
        SetProperty(pickup, "openAudio", p => p.objectReferenceValue = open);
    }

    static void PlaceBraziers(Tilemap level, Transform root)
    {
        AudioClip hurt = LoadAudio("Hurt");

        GameObject first = Spawn("PF Village Props - Fire Bowl", new Vector3(-12f, SurfaceY(level, -12f, BrinstarMain), 0f), root, "Brasero_Peligro");
        SetupDamageZone(first, GameElement.Fuego, hurt);

        GameObject second = Spawn("PF Village Props - Fire Bowl", new Vector3(40f, SurfaceY(level, 40f, TopLevel), 0f), root, "Brasero_Elemento");
        SetupDamageZone(second, GameElement.Rayo, hurt);
    }

    static void SetupDamageZone(GameObject brazier, GameElement safeElement, AudioClip hurt)
    {
        if (brazier == null) return;

        BoxCollider2D collider = brazier.GetComponent<BoxCollider2D>();
        if (collider == null) collider = brazier.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(1.6f, 2.4f);
        collider.offset = new Vector2(0f, 1f);

        DamageZone zone = brazier.GetComponent<DamageZone>();
        if (zone == null) zone = brazier.AddComponent<DamageZone>();

        SetProperty(zone, "safeElement", p => p.enumValueIndex = (int)safeElement);
        SetProperty(zone, "hurtAudio", p => p.objectReferenceValue = hurt);
    }

    static void PlaceElementalWall(Tilemap level, Transform root)
    {
        float baseY = SurfaceY(level, WallX, TopLevel);

        GameObject wall = new GameObject("Pared_Elemental");
        wall.transform.SetParent(root, false);
        wall.transform.position = new Vector3(WallX, baseY, 0f);

        for (int i = 0; i < 6; i++)
        {
            GameObject brick = Spawn("PF Village Props - Brick Wall 01", Vector3.zero, wall.transform, "Ladrillo_" + (i + 1));
            if (brick == null) continue;
            brick.transform.localPosition = new Vector3(0f, 0.67f + i * 1.34f, 0f);
        }

        GameObject blocker = new GameObject("Bloqueo");
        blocker.transform.SetParent(wall.transform, false);
        blocker.transform.localPosition = Vector3.zero;
        BoxCollider2D blockerCollider = blocker.AddComponent<BoxCollider2D>();
        blockerCollider.size = new Vector2(4.4f, 8f);
        blockerCollider.offset = new Vector2(0f, 4f);

        // El aviso es un trigger en el mismo objeto que ElementalWall: si esta
        // en un hijo, Unity no le envia los eventos de trigger al script.
        BoxCollider2D warning = wall.AddComponent<BoxCollider2D>();
        warning.isTrigger = true;
        warning.size = new Vector2(2.4f, 4f);
        warning.offset = new Vector2(-2.8f, 4f);

        ElementalWall element = wall.AddComponent<ElementalWall>();
        SetProperty(element, "requiredElement", p => p.enumValueIndex = (int)GameElement.Rayo);
        SetProperty(element, "openAudio", p => p.objectReferenceValue = LoadAudio("Collectable"));
        SetProperty(element, "blockedAudio", p => p.objectReferenceValue = LoadAudio("Hurt"));
    }

    static void SetupEntities(Tilemap level)
    {
        GameObject oldBoss = FindSceneObject("Jefe");
        if (oldBoss != null) UnityEngine.Object.DestroyImmediate(oldBoss);
        GameObject oldBossPath = FindSceneObject("BossPatrolPath");
        if (oldBossPath != null) UnityEngine.Object.DestroyImmediate(oldBossPath);

        // Jugador
        PlayerController player = UnityEngine.Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player != null)
        {
            SetFeet(player.gameObject, new Vector2(PlayerStartX, SurfaceY(level, PlayerStartX, StartLevel)));
            SpriteRenderer sr = player.GetComponent<SpriteRenderer>();
            if (sr != null) sr.flipX = false;

            Health health = player.GetComponent<Health>();
            if (health != null) SetProperty(health, "maxHP", p => p.intValue = 3);

            if (player.GetComponent<PlayerAbilities>() == null)
                player.gameObject.AddComponent<PlayerAbilities>();

            PlayerMeleeAttack attack = player.GetComponent<PlayerMeleeAttack>();
            if (attack == null) attack = player.gameObject.AddComponent<PlayerMeleeAttack>();
            SetProperty(attack, "attackAudio", p => p.objectReferenceValue = LoadAudio("LandOnEnemy"));

            Debug.Log("[VillagePropsMigrator] Jugador preparado en " + player.transform.position);
        }
        else Debug.LogWarning("[VillagePropsMigrator] No se encontro PlayerController.");

        // Enemigos
        List<EnemyController> enemyList = new List<EnemyController>();
        foreach (EnemyController ec in UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Exclude))
        {
            if (ec.gameObject.name != "Jefe" && ec.gameObject.name != "MartialHeroBoss") enemyList.Add(ec);
        }

        // Orden estable para que la colocación sea siempre la misma.
        enemyList.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

        float[] enemyX = { -26f, -18f, -6f, 28f, 38f, 48f };
        float[] enemyCeiling = { StartLevel, BrinstarHigh, BrinstarMain, MaridiaMain, MaridiaLow, NorfairLevel };
        float[] patrol = { 2f, 2f, 2f, 2f, 1f, 2f };

        // El nivel ampliado necesita mas enemigos que la escena original.
        Transform enemiesRoot = GameObject.Find("Enemies") != null ? GameObject.Find("Enemies").transform : null;
        while (enemyList.Count > 0 && enemyList.Count < enemyX.Length)
        {
            GameObject clone = (GameObject)UnityEngine.Object.Instantiate(enemyList[0].gameObject);
            clone.name = "Enemy_" + (enemyList.Count + 1);
            if (enemiesRoot != null) clone.transform.SetParent(enemiesRoot, true);

            EnemyController cloneController = clone.GetComponent<EnemyController>();
            if (cloneController == null) break;
            enemyList.Add(cloneController);
        }

        for (int i = 0; i < enemyList.Count; i++)
        {
            EnemyController e = enemyList[i];
            if (i < enemyX.Length)
            {
                float y = SurfaceY(level, enemyX[i], enemyCeiling[i]);
                SetFeet(e.gameObject, new Vector2(enemyX[i], y));
                e.gameObject.SetActive(true);

                if (e.Path != null)
                {
                    e.Path.startPosition = new Vector2(enemyX[i] - patrol[i], y);
                    e.Path.endPosition = new Vector2(enemyX[i] + patrol[i], y);
                }
            }
            else
            {
                e.gameObject.SetActive(false);
            }
        }

        ConfigureFinalBoss(level, enemiesRoot);

        // Monedas
        List<TokenInstance> tokenList = new List<TokenInstance>(UnityEngine.Object.FindObjectsByType<TokenInstance>(FindObjectsInactive.Include));
        tokenList.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

        TokenInstance[] tokens = tokenList.ToArray();
        for (int i = 0; i < tokens.Length; i++)
        {
            if (i < TokenSpots.Length)
            {
                TokenSpot spot = TokenSpots[i];
                tokens[i].gameObject.SetActive(true);
                tokens[i].transform.position = new Vector3(spot.x, SurfaceY(level, spot.x, spot.ceiling) + spot.height, 0f);

                foreach (SpriteRenderer sr in tokens[i].GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sr.sortingOrder < TokenSortingOrder) sr.sortingOrder = TokenSortingOrder;
                }

                RecordInstance(tokens[i].gameObject);
            }
            else
            {
                tokens[i].gameObject.SetActive(false);
            }
        }
        Debug.Log("[VillagePropsMigrator] Monedas colocadas: " + Mathf.Min(tokens.Length, TokenSpots.Length));

        // Zona de muerte
        DeathZone[] zones = UnityEngine.Object.FindObjectsByType<DeathZone>(FindObjectsInactive.Include);
        for (int i = 0; i < zones.Length; i++)
        {
            if (i == 0)
            {
                zones[i].gameObject.SetActive(true);
                zones[i].transform.position = new Vector3(10f, -11f, 0f);
                zones[i].transform.localScale = new Vector3(115f, 1f, 1f);
                RecordInstance(zones[i].gameObject);
            }
            else zones[i].gameObject.SetActive(false);
        }

        // Punto de reaparicion
        GameObject spawnObject = GameObject.Find("SpawnPoint");
        if (spawnObject != null)
        {
            if (spawnObject.GetComponent<SpawnPoint>() == null)
                spawnObject.AddComponent<SpawnPoint>();
            spawnObject.transform.position = new Vector3(PlayerStartX, SurfaceY(level, PlayerStartX, StartLevel) + 0.41f, 0f);
            RecordInstance(spawnObject);
        }

        // Zona de victoria
        VictoryZone victory = UnityEngine.Object.FindAnyObjectByType<VictoryZone>(FindObjectsInactive.Include);
        if (victory != null)
        {
            victory.transform.position = new Vector3(VictoryX, SurfaceY(level, VictoryX, TopLevel) + 1f, 0f);
            RecordInstance(victory.gameObject);
        }
    }

    static void ConfigureFinalBoss(Tilemap level, Transform enemiesRoot)
    {
        GameObject boss = FindSceneObject("MartialHeroBoss");
        if (boss == null)
        {
            Debug.LogWarning("[VillagePropsMigrator] No se encontro MartialHeroBoss.");
            return;
        }

        boss.SetActive(true);
        boss.transform.localScale = new Vector3(2.2f, 2.2f, 2.2f);

        const float bossX = -26.27f;
        const float bossY = -7.4f;
        boss.transform.position = new Vector3(bossX, bossY, 1f);

        BoxCollider2D box = boss.GetComponent<BoxCollider2D>();
        if (box == null) box = boss.AddComponent<BoxCollider2D>();
        box.size = new Vector2(1.2f, 1.6f);
        box.offset = new Vector2(0f, -0.2f);

        AnimationController anim = boss.GetComponent<AnimationController>();
        if (anim != null)
        {
            SetProperty(anim, "maxSpeed", p => p.floatValue = 3f);
            SetProperty(anim, "gravityModifier", p => p.floatValue = 0f);
        }

        EnemyController enemy = boss.GetComponent<EnemyController>();
        if (enemy == null) enemy = boss.AddComponent<EnemyController>();
        enemy.enabled = true;
        enemy.Path = null;

        GameObject oldPath = FindSceneObject("MartialHeroBossPatrolPath");
        if (oldPath != null) UnityEngine.Object.DestroyImmediate(oldPath);

        BossController combat = boss.GetComponent<BossController>();
        if (combat == null) combat = boss.AddComponent<BossController>();

        BossPatrolMover mover = boss.GetComponent<BossPatrolMover>();
        if (mover == null) mover = boss.AddComponent<BossPatrolMover>();
        SetProperty(mover, "patrolRadius", p => p.floatValue = 1.5f);
        SetProperty(mover, "speed", p => p.floatValue = 1.5f);
        SetProperty(mover, "chaseSpeed", p => p.floatValue = 2.6f);
        SetProperty(mover, "stopDistance", p => p.floatValue = 1.6f);
        SetProperty(mover, "chaseRadius", p => p.floatValue = 18f);

        BossFireAttack fire = boss.GetComponent<BossFireAttack>();
        if (fire == null) fire = boss.AddComponent<BossFireAttack>();
        SetProperty(fire, "boss", p => p.objectReferenceValue = combat);
        SetProperty(fire, "fireInterval", p => p.floatValue = 4.5f);
        SetProperty(fire, "attackRange", p => p.floatValue = 14f);
        SetProperty(fire, "damage", p => p.intValue = 1);
        SetProperty(fire, "projectileSpeed", p => p.floatValue = 8f);
        SetProperty(fire, "projectileRange", p => p.floatValue = 16f);
        SetProperty(fire, "hitRadius", p => p.floatValue = 0.55f);
        SetProperty(fire, "fireSprite", p => p.objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Cainos/Pixel Art Platformer - Village Props/Texture/TX FX Flame.png"));
        SetProperty(fire, "impactVfx", p => p.objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Fire/CFXR3 Hit Fire B (Air).prefab"));

        SetProperty(combat, "bossName", p => p.stringValue = "MARTIAL HERO");
        SetProperty(combat, "aggroRadius", p => p.floatValue = 12f);
        SetProperty(combat, "autoStart", p => p.boolValue = false);
        SetProperty(combat, "bossTheme", p => p.objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Ninjago - Hands of Time (Epic).mp3"));

        GameController gameController = UnityEngine.Object.FindAnyObjectByType<GameController>();
        if (gameController != null)
        {
            GameCinematicController cinematic = gameController.GetComponent<GameCinematicController>();
            if (cinematic == null) cinematic = gameController.gameObject.AddComponent<GameCinematicController>();
            SetProperty(cinematic, "boss", p => p.objectReferenceValue = boss);
            SetProperty(cinematic, "cyberPortrait", p => p.objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Cyberpunk Character Pack/Portraits/character_10.png"));
        }

        RecordInstance(boss);
        Debug.Log("[VillagePropsMigrator] MartialHeroBoss preparado en " + boss.transform.position);
    }

    static GameObject FindSceneObject(string name)
    {
        Scene scene = SceneManager.GetActiveScene();
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.scene == scene && go.name == name) return go;
        }
        return null;
    }

    static void SetupHud()
    {
        GameObject old = GameObject.Find(HudName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);

        GameObject root = new GameObject(HudName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(null);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        CreateHudText(root.transform, "Tokens", new Vector2(30f, -30f), new Vector2(520f, 60f), 34f, TextAlignmentOptions.Left, font);
        CreateHudText(root.transform, "Elemento", new Vector2(30f, -90f), new Vector2(520f, 60f), 34f, TextAlignmentOptions.Left, font);
        CreateHudText(root.transform, "Vida", new Vector2(30f, -150f), new Vector2(520f, 60f), 34f, TextAlignmentOptions.Left, font);
        CreateHudText(root.transform, "Mensaje", new Vector2(0f, 50f), new Vector2(1400f, 70f), 38f, TextAlignmentOptions.Center, font, true);

        root.AddComponent<HudController>();
    }

    static void CreateHudText(Transform parent, string name, Vector2 position, Vector2 size, float fontSize,
        TextAlignmentOptions alignment, TMP_FontAsset font, bool bottom = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = bottom ? new Vector2(0.5f, 0f) : new Vector2(0f, 1f);
        rect.anchorMax = rect.anchorMin;
        rect.pivot = bottom ? new Vector2(0.5f, 0f) : new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color(0f, 0f, 0f, 0.85f);
        text.raycastTarget = false;
        text.text = string.Empty;
    }

    static void ConfigureModel()
    {
        GameController controller = UnityEngine.Object.FindAnyObjectByType<GameController>(FindObjectsInactive.Include);
        if (controller != null && controller.model != null)
        {
            controller.model.jumpModifier = JumpModifier;
            controller.model.jumpDeceleration = JumpDeceleration;
        }

        PlatformerModel shared = Simulation.GetModel<PlatformerModel>();
        if (shared != null)
        {
            shared.jumpModifier = JumpModifier;
            shared.jumpDeceleration = JumpDeceleration;
        }

        // Fisica del jugador: velocidad horizontal y salto ajustados a los
        // saltos del mapa (hueco 2, desnivel 2). Con la velocidad anterior el
        // salto por encima de +2 unidades duraba 1,54 s (9,2 u de recorrido) y
        // el jugador sobrevolaba las plataformas de 4 u, cayendo al vacio.
        PlayerController player = UnityEngine.Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player != null)
        {
            SetProperty(player, "maxSpeed", p => p.floatValue = PlayerMaxSpeed);
            SetProperty(player, "jumpTakeOffSpeed", p => p.floatValue = PlayerJumpTakeOffSpeed);
            PrefabUtility.RecordPrefabInstancePropertyModifications(player);
            RecordInstance(player.gameObject);

            Debug.Log($"[VillagePropsMigrator] Fisica del jugador: maxSpeed={PlayerMaxSpeed} " +
                      $"jumpTakeOffSpeed={PlayerJumpTakeOffSpeed} jumpModifier={JumpModifier} " +
                      $"v0={PlayerJumpTakeOffSpeed * JumpModifier:0.##} " +
                      $"v0 real={GetFloat(player, "jumpTakeOffSpeed") * JumpModifier:0.##}");
        }

        // Vida y fisica del jugador en el prefab. Enemy.prefab es un prefab
        // independiente (no es una variante), asi que los enemigos no cambian.
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (prefab != null)
        {
            Health health = prefab.GetComponent<Health>();
            if (health != null)
            {
                SetProperty(health, "maxHP", p => p.intValue = 3);
                EditorUtility.SetDirty(health);
            }

            PlayerController prefabPlayer = prefab.GetComponent<PlayerController>();
            if (prefabPlayer != null)
            {
                SetProperty(prefabPlayer, "maxSpeed", p => p.floatValue = PlayerMaxSpeed);
                SetProperty(prefabPlayer, "jumpTakeOffSpeed", p => p.floatValue = PlayerJumpTakeOffSpeed);
                EditorUtility.SetDirty(prefabPlayer);
            }

            PrefabUtility.SavePrefabAsset(prefab);
        }
    }

    static void UpdateConfiner()
    {
        GameObject confiner = GameObject.Find("CinemachineConfiner");
        if (confiner == null) return;

        PolygonCollider2D polygon = confiner.GetComponent<PolygonCollider2D>();
        if (polygon == null) return;

        Vector3 origin = confiner.transform.position;
        polygon.points = new[]
        {
            new Vector2(-44f - origin.x, 62f - origin.y),
            new Vector2(64f - origin.x, 62f - origin.y),
            new Vector2(64f - origin.x, -12f - origin.y),
            new Vector2(-44f - origin.x, -12f - origin.y),
        };
    }

    static void SetFeet(GameObject go, Vector2 feet)
    {
        Collider2D col = MainCollider(go);
        float halfHeight = 0.25f;
        float offsetY = 0f;

        if (col != null)
        {
            float scale = Mathf.Abs(col.transform.lossyScale.y);
            halfHeight = ColliderHeight(col) * scale * 0.5f;
            offsetY = col.offset.y * scale;
        }

        go.transform.position = new Vector3(feet.x, feet.y - offsetY + halfHeight, go.transform.position.z);
        RecordInstance(go);
    }

    /// <summary>
    /// Collider solido mas alto del objeto (el que define los pies).
    /// </summary>
    static Collider2D MainCollider(GameObject go)
    {
        Collider2D best = null;
        float bestSize = -1f;

        foreach (Collider2D col in go.GetComponents<Collider2D>())
        {
            if (col.isTrigger) continue;
            float size = ColliderHeight(col) * Mathf.Abs(col.transform.lossyScale.y);
            if (size > bestSize)
            {
                bestSize = size;
                best = col;
            }
        }

        if (best != null) return best;

        foreach (Collider2D col in go.GetComponents<Collider2D>())
        {
            float size = ColliderHeight(col) * Mathf.Abs(col.transform.lossyScale.y);
            if (size > bestSize)
            {
                bestSize = size;
                best = col;
            }
        }

        return best;
    }

    /// <summary>
    /// Altura local del collider, independientemente del tipo.
    /// </summary>
    static float ColliderHeight(Collider2D col)
    {
        BoxCollider2D box = col as BoxCollider2D;
        if (box != null) return box.size.y;

        CapsuleCollider2D capsule = col as CapsuleCollider2D;
        if (capsule != null) return capsule.size.y;

        CircleCollider2D circle = col as CircleCollider2D;
        if (circle != null) return circle.radius * 2f;

        return col.bounds.size.y;
    }

    /// <summary>
    /// Quita los componentes SceneObjectGuid que el framework de tutoriales
    /// (com.unity.learn.iet-framework) deja en los objetos de la escena.
    /// Si se mantienen, al reabrir la escena en la misma sesión el gestor
    /// registra dos veces la misma clave y lanza una excepción.
    /// </summary>
    static void RemoveSceneObjectGuids()
    {
        int removed = 0;

        foreach (GameObject go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
        {
            foreach (Component component in go.GetComponents<Component>())
            {
                if (component == null) continue;
                if (component.GetType().Name != "SceneObjectGuid") continue;

                UnityEngine.Object.DestroyImmediate(component, true);
                removed++;
            }
        }

        if (removed > 0)
            Debug.Log("[VillagePropsMigrator] SceneObjectGuid eliminados: " + removed);
    }

    /// <summary>
    /// Guarda en la escena los cambios hechos por codigo sobre instancias de prefab.
    /// </summary>
    static void RecordInstance(GameObject go)
    {
        if (go == null) return;
        if (!PrefabUtility.IsPartOfPrefabInstance(go)) return;
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
    }

    /// <summary>
    /// Refresca las modificaciones de todas las instancias de prefab de la escena.
    /// </summary>
    static void RecordAllInstances()
    {
        foreach (GameObject go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(go)) continue;
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        }
    }

    static void SetProperty(UnityEngine.Object target, string propertyName, Action<SerializedProperty> apply)
    {
        if (target == null) return;

        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogWarning($"[VillagePropsMigrator] No se encontro la propiedad '{propertyName}' en {target.GetType().Name}.");
            return;
        }

        apply(property);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Valor actual de una propiedad serializada (para comprobar los cambios).</summary>
    static float GetFloat(UnityEngine.Object target, string propertyName)
    {
        if (target == null) return 0f;

        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(propertyName);
        return property == null ? 0f : property.floatValue;
    }

    static AudioClip LoadAudio(string clipName)
    {
        return AssetDatabase.LoadAssetAtPath<AudioClip>(AudioBase + clipName + ".wav");
    }

    static GameObject Spawn(string prefabName, Vector3 position, Transform parent, string objName)
    {
        string path = PrefabBase + prefabName + ".prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning("[VillagePropsMigrator] Prefab no encontrado: " + path);
            return null;
        }

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.name = objName;

        foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>(true))
        {
            // Los props van por detrás del jugador (5) y de las monedas (4).
            if (sr.sortingOrder < PropSortingOrder) sr.sortingOrder = PropSortingOrder;
        }

        RecordInstance(go);
        return go;
    }
}
