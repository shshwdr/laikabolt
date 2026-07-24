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
    [Tooltip("Food items dropped when an enemy is killed.")]
    public int enemyFoodDrop;
    [Tooltip("Bonus per deposited food: score = count * (1 + foodCollectAmount).")]
    public int foodCollectAmount;
    [Tooltip("Spawn a collecting robot at run start.")]
    public bool machineCollect;
    [Tooltip("Seconds between each food grab by the collect robot.")]
    public float machineCollectInterval = 1f;
    [Tooltip("Percent chance to spawn one extra stacked food when generating food.")]
    public int bonusGenerateChance;

    [Header("Combat")]
    public int enemyHitsToKill = 3;

    [Header("Grid / Move")]
    public float cellSize = 1f;
    public float moveDuration = 0.1f;
    public float jumpDuration = 0.28f;
    public float jumpPower = 0.85f;

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
