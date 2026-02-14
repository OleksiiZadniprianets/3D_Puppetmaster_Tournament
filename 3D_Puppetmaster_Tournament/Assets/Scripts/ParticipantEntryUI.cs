using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ParticipantEntryUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Image avatarImage;
    [SerializeField] private TMP_Text nameText;

    [Header("Optional Visuals")]
    [SerializeField] private Image frameImage;
    [SerializeField] private Image dimOverlay;

    private Color _defaultFrameColor = Color.white;

    private void Awake()
    {
        if (frameImage != null) _defaultFrameColor = frameImage.color;
    }

    public void Setup(string displayName, Sprite avatar)
    {
        if (nameText != null) nameText.text = displayName;

        if (avatarImage != null)
        {
            avatarImage.sprite = avatar;
            avatarImage.preserveAspect = true;
            avatarImage.color = Color.white;
        }

        SetEliminated(false);
        SetHighlighted(false);
        SetActiveMatchSide(false, false);
    }

    public void SetEliminated(bool eliminated)
    {
        if (avatarImage != null)
            avatarImage.color = eliminated ? new Color(0.35f, 0.35f, 0.35f, 1f) : Color.white;

        if (nameText != null)
            nameText.alpha = eliminated ? 0.55f : 1f;

        if (dimOverlay != null)
            dimOverlay.gameObject.SetActive(eliminated);
    }

    public void SetHighlighted(bool highlighted)
    {
        if (frameImage != null)
            frameImage.color = highlighted ? new Color(1f, 0.95f, 0.4f, 1f) : _defaultFrameColor;
    }

    public void SetActiveMatchSide(bool isLeft, bool isRight)
    {
        float scale = (isLeft || isRight) ? 1.08f : 1f;
        transform.localScale = new Vector3(scale, scale, 1f);
    }
}
