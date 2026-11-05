using System;
using System.Collections.Generic;
using UnityEngine;

namespace YG
{
    [Serializable]
    public class BossRecord
    {
        public string bossId;
        public float bestTime;
        public bool defeated;

        public BossRecord(string id, float time)
        {
            bossId = id;
            bestTime = time;
            defeated = true;
        }
    }

    [Serializable]
    public class PlayerData
    {
        [Header("Кошелёк")]
        public Wallet wallet = new Wallet();

        [Header("Кирки")]
        public List<string> boughtPickaxes = new List<string>();
        public string equippedPickaxeId = string.Empty;

        [Header("Уровень / опыт")]
        public int playerLevel = 1;
        public float levelExperience = 0f;
        public float totalExperience = 0f;
        public int unlockedPickaxeCount = 1;

        [Header("Перерождение")]
        [Tooltip("Количество выполненных перерождений.")]
        public int rebirthCount = 0;

        [Tooltip("Текущий множитель урона кирок.")]
        public float pickaxeDamageMultiplier = 1f;

        [Header("Следы")]
        public List<string> boughtTrails = new List<string>();
        public string equippedTrailId = string.Empty;

        [Header("Тренировочные куклы")]
        [Tooltip("ID всех разблокированных тренировочных кукол.")]
        public List<string> unlockedDummies = new List<string>();

        [Header("Ауры")]
        public string equippedAuraId = "";
        public bool isAuraEnabled = true;

        public int luckySpins = 0;

        public bool hasDoubleWins;
        public bool hasDoubleDamage;

        [Header("Миры")]
        public List<string> purchasedWorldIds = new List<string>();

        [Header("Обучение")]
        public bool tutorialCompleted = false;

        [Header("Боссы")]
        public List<BossRecord> bossRecords = new List<BossRecord>();

        public void EnsureValidData()
        {
            if (wallet == null)
                wallet = new Wallet();

            if (boughtPickaxes == null)
                boughtPickaxes = new List<string>();

            if (playerLevel < 1)
                playerLevel = 1;

            if (levelExperience < 0f)
                levelExperience = 0f;

            if (totalExperience < 0f)
                totalExperience = 0f;

            if (unlockedPickaxeCount < 1)
                unlockedPickaxeCount = 1;

            if (rebirthCount < 0)
                rebirthCount = 0;

            if (pickaxeDamageMultiplier < 1f)
                pickaxeDamageMultiplier = 1f;

            if (!boughtPickaxes.Contains("pickaxe_01"))
                boughtPickaxes.Add("pickaxe_01");

            if (!boughtPickaxes.Contains("pickaxe_01_w"))
                boughtPickaxes.Add("pickaxe_01_w");

            if (string.IsNullOrEmpty(equippedPickaxeId))
                equippedPickaxeId = "pickaxe_01";

            if (!boughtPickaxes.Contains(equippedPickaxeId))
                equippedPickaxeId = "pickaxe_01";

            if (boughtTrails == null)
                boughtTrails = new List<string>();

            if (equippedTrailId == null)
                equippedTrailId = string.Empty;

            if (!string.IsNullOrEmpty(equippedTrailId) &&
                !boughtTrails.Contains(equippedTrailId))
            {
                equippedTrailId = string.Empty;
            }

            if (unlockedDummies == null)
                unlockedDummies = new List<string>();

            if (equippedAuraId == null)
                equippedAuraId = string.Empty;

            if (purchasedWorldIds == null)
                purchasedWorldIds = new List<string>();

            if (bossRecords == null)
                bossRecords = new List<BossRecord>();
        }
    }

    [Serializable]
    public class Wallet
    {
        public long money;
        public long diamonds;
        public long wins;
        public long spins;
    }

    [System.Serializable]
    public class Settings
    {
        public float sensitivity = 400f;
        public float volume = 1f;
        public float musicVolume = 0.35f;
    }

    [Serializable]
    public partial class SavesYG
    {
        public int idSave;
        public PlayerData playerData;
        public Settings settings;

        public SavesYG()
        {
            playerData = new PlayerData();
            playerData.EnsureValidData();

            settings = new Settings();
        }
    }
}