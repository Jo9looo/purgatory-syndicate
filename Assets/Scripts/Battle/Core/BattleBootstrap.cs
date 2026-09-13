using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PurgatorySyndicate.Battle
{
    /// <summary>
    /// Builds a playable battle from EncounterData whenever the battle scene loads —
    /// both on direct Play and when arriving from the main menu.
    /// Skipped when the scene already contains a BattleManager.
    /// Old visual placeholders (Ally_1 / Enemy_1) are disabled to avoid duplicates.
    /// </summary>
    public static class BattleBootstrap
    {
        private const string BattleSceneName = "SampleScene";
        private static readonly Color[] AllyColors =
        {
            new Color(0.25f, 0.55f, 1f),
            new Color(0.3f, 0.85f, 0.9f),
            new Color(0.5f, 0.6f, 1f),
            new Color(0.4f, 0.75f, 0.65f)
        };

        private static readonly Color[] EnemyColors =
        {
            new Color(0.9f, 0.25f, 0.25f),
            new Color(1f, 0.5f, 0.2f),
            new Color(0.8f, 0.2f, 0.6f),
            new Color(0.7f, 0.25f, 0.35f)
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            // RuntimeInitializeOnLoadMethod only fires once at startup, so we also
            // listen for scene loads (e.g., entering battle from the main menu).
            SceneManager.sceneLoaded += OnSceneLoaded;
            TrySetup(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TrySetup(scene);
        }

        private static void TrySetup(Scene scene)
        {
            // Only the battle scene gets a battle; menu and future scenes are skipped.
            if (scene.name != BattleSceneName)
            {
                return;
            }

            if (Object.FindAnyObjectByType<BattleManager>() != null)
            {
                Debug.Log("[BattleBootstrap] BattleManager sudah ada di scene — auto-setup dilewati.");
                return;
            }

            EncounterData encounter = BattleProgression.LoadCurrent();
            if (encounter == null)
            {
                Debug.LogError("[BattleBootstrap] Encounter stage tidak ditemukan di Resources/Battle/.");
                return;
            }

            Debug.Log($"[BattleBootstrap] Stage {BattleProgression.StageNumber}/{BattleProgression.StageCount}: {encounter.encounterName}");

            // Old whitebox placeholders are visual-only; hide them so the arena stays clean.
            DisablePlaceholder("Ally_1");
            DisablePlaceholder("Enemy_1");
            EnsureGround();

            List<BattleEntity> entities = new List<BattleEntity>();

            for (int i = 0; i < encounter.allies.Count; i++)
            {
                Vector3 pos = new Vector3(-2f - i * 0.5f, 1f, 1.4f - i * 1.7f);
                entities.Add(SpawnCombatant(encounter.allies[i], EntityTeam.Ally, pos,
                    AllyColors[i % AllyColors.Length]));
            }

            for (int i = 0; i < encounter.enemies.Count; i++)
            {
                Vector3 pos = new Vector3(2f + i * 0.5f, 1f, 1.4f - i * 1.7f);
                entities.Add(SpawnCombatant(encounter.enemies[i], EntityTeam.Enemy, pos,
                    EnemyColors[i % EnemyColors.Length]));
            }

            GameObject managerObject = new GameObject("BattleManager");
            managerObject.AddComponent<TargetingController>();
            BattleManager manager = managerObject.AddComponent<BattleManager>();
            managerObject.AddComponent<BattleMouseController>();
            managerObject.AddComponent<BattleInputController>();
            manager.RegisterCombatants(entities, encounter.encounterName);

            new GameObject("BattleHUD").AddComponent<BattleHUD>();

            Debug.Log("[BattleBootstrap] Siap! Pilih skill (1-3), target (Tab), konfirmasi (Enter).");
        }

        private static BattleEntity SpawnCombatant(
            CharacterDefinition definition,
            EntityTeam team,
            Vector3 position,
            Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            string charName = definition != null && definition.baseStats != null
                ? definition.baseStats.characterName
                : "Unknown";
            go.name = $"{team}_{charName}";
            go.transform.position = position;

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
            }

            BattleEntity entity = go.AddComponent<BattleEntity>();
            entity.Configure(definition, team);

            // Motion component drives whitebox attack animations (lunge/shake/knockback).
            go.AddComponent<BattleEntityMotion>().CaptureHome();

            return entity;
        }

        private static void DisablePlaceholder(string objectName)
        {
            GameObject go = GameObject.Find(objectName);
            if (go != null && go.GetComponent<BattleEntity>() == null)
            {
                go.SetActive(false);
            }
        }

        private static void EnsureGround()
        {
            if (GameObject.Find("Arena") != null || GameObject.Find("TestGround") != null)
            {
                return;
            }

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "TestGround";
            ground.transform.localScale = new Vector3(0.8f, 1f, 0.8f);

            Renderer renderer = ground.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.15f, 0.17f, 0.2f);
            }
        }
    }
}
