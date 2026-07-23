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

    [Header("Combat")]
    public int enemyHitsToKill = 3;

    [Header("Grid / Move")]
    public float cellSize = 1f;
    public float moveDuration = 0.1f;

    [Header("Carry Visual")]
    [Tooltip("Scale of food while carried on the player.")]
    public float carryScale = 0.5f;
    [Tooltip("Local Y of the first carried food.")]
    public float carryBaseY = 0.35f;
    [Tooltip("Extra local Y added for each stacked food.")]
    public float carryStackHeightStep = 0.22f;

    [Header("Enemy Fly Away")]
    public float enemyFlyDuration = 0.45f;
    public float enemyFlyJumpPower = 1.2f;
    public float enemyFlyDistance = 2.5f;

    [Header("Sprites (optional overrides; else Resources/render)")]
    public Sprite playerSprite;
    public Sprite foodSprite;
    public Sprite monsterSprite;
    public Sprite tileSprite;
}
