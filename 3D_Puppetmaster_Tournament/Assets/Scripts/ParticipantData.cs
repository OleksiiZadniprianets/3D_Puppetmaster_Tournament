using UnityEngine;

[System.Serializable]
public class ParticipantData
{
    public TournamentParticipant baseData;
    [HideInInspector] public bool eliminated;
}
