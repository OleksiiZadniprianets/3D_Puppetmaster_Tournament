using UnityEngine;

[System.Serializable]
public class TournamentParticipant
{
    public int baseHP;
    public int baseDamage;

    public string name;
    public Sprite avatar;

    [Header("Base Stats (will be randomized at start)")]
    public int damage;
    public int maxHP;

    [Header("Runtime (do not edit)")]
    [HideInInspector] public int currentHP;
}
