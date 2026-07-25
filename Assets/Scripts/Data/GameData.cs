using UnityEngine;

[CreateAssetMenu(fileName = "GameData", menuName = "GMTK/Game Data", order = 1)]
public class GameData : ScriptableObject
{
    [Header("Round")]
    public float roundDuration = 20f;

    [Header("Spawn")]
    public float collectableSpawnInterval = 1f;
    public float enemySpawnInterval = 5f;
    public int initialCollectables = 3;
    public int initialEnemies = 1;

    [Header("Player")]
    [Tooltip("Max collectables the player can carry at once.")]
    public int holdItemCount = 3;
    [Tooltip("Damage dealt per bump into an enemy.")]
    public int playerHitDamage = 1;
    [Tooltip("Max consecutive Blocked(o) cells the player can jump over.")]
    public int jumpDistance = 0;
    [Tooltip("Wrap around map edges when moving off-bounds.")]
    public bool passBorder;
    [Tooltip("Guaranteed food dropped when an enemy is killed.")]
    public int enemyFoodDrop = 1;
    [Tooltip("Extra food amount on successful chance roll (from enemyFood upgrades). Roll only happens if > 0.")]
    public int enemyFoodBonus;
    [Tooltip("Percent chance for enemyFoodBonus extra drop (base 20%; enemyFoodChance upgrades add more). Only used when enemyFoodBonus > 0.")]
    public int enemyFoodChance = 20;
    [Tooltip("Bonus per deposited food: score = count * (1 + foodCollectAmount).")]
    public int foodCollectAmount;
    [Tooltip("Spawn collecting robot(s) at run start.")]
    public bool machineCollect;
    [Tooltip("How many collect robots to spawn (sum of machineCollect upgrade levels).")]
    public int machineCollectCount;
    [Tooltip("Seconds between each food grab by the collect robot.")]
    public float machineCollectInterval = 1f;
    [Tooltip("Extra ore amount on successful spawn chance roll (from bonusGenerate). Roll only if > 0.")]
    public int bonusGenerateBonus;
    [Tooltip("Percent chance for bonusGenerateBonus extra spawn (base 20%; bonusGenerateChance upgrades add more). Only used when bonusGenerateBonus > 0.")]
    public int bonusGenerateChance = 20;

    [Header("Upgrade Effects")]
    [Tooltip("When time expires on Start/spaceship, Score += Score * percent / 100.")]
    public int finalSafePercent;
    [Tooltip("When delivering to spaceship, flat bonus ore (value x level from upgrade).")]
    public int fullRewardBonus;
    [Tooltip("When remaining time is below 5s, each ground pickup adds a second food to hand.")]
    public bool lastMinute;
    [Tooltip("Long-press a direction to dash until hole, enemy, or map edge.")]
    public bool dash;
    [Tooltip("Jump onto an enemy for 2x damage, then jump back.")]
    public bool jumpAttack;
    [Tooltip("Dash into the enemy ahead for 2x damage.")]
    public bool dashAttack;
    [Tooltip("When depositing food at the spaceship, damage all enemies.")]
    public bool homeAttack;
    [Tooltip("When hitting an enemy, pull this many adjacent ore to the player (value x level).")]
    public int attackAttractCount;

    [Header("Combat")]
    public int enemyHitsToKill = 3;

    [Header("Grid / Move")]
    public float cellSize = 1f;
    public float moveDuration = 0.1f;
    public float jumpDuration = 0.28f;
    public float jumpPower = 0.85f;
    [Tooltip("Hold duration before dash triggers.")]
    public float dashHoldSeconds = 0.5f;
    [Tooltip("Tween duration for a dash (very fast).")]
    public float dashDuration = 0.06f;

    [Header("Carry Visual")]
    [Tooltip("Scale of food while carried on the player.")]
    public float carryScale = 0.5f;
    [Tooltip("Local Y of the first carried food.")]
    public float carryBaseY = 0.35f;
    [Tooltip("Extra local Y added for each food stacked on player/robot.")]
    public float carryStackHeightStep = 0.22f;
    [Tooltip("Extra world Y added for each food stacked on the ground.")]
    public float groundStackHeightStep = 0.25f;

    [Header("Enemy Fly Away")]
    public float enemyFlyDuration = 0.45f;
    public float enemyFlyJumpPower = 1.2f;
    public float enemyFlyDistance = 2.5f;

    [Header("Sprites (optional overrides; else Resources/render)")]
    public Sprite playerSprite;
    public Sprite foodSprite;
    public Sprite monsterSprite;
    public Sprite robotSprite;
    public Sprite tileSprite;
}
