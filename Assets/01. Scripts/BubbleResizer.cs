using System.Threading.Tasks;
using TMPro;
using UnityEngine;

public class BubbleResizer : MonoBehaviour
{
    public TextMeshProUGUI MsgText;
    public RectTransform   BubbleRt;
    public float           MaxWidth     = 1500f; // 최대 말풍선 너비
    public float           WidthOffset  = 100f;
    public float           HeightOffset = 20f;

    public  void Resize(string text)
    {
        MsgText.text = text;

        float txtWidth = MsgText.preferredWidth;
        float width = Mathf.Min(txtWidth, MaxWidth);
        
        MsgText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        MsgText.textWrappingMode = TextWrappingModes.Normal;

        float txtHeight = MsgText.preferredHeight;

        BubbleRt.sizeDelta = new Vector2(width + WidthOffset, txtHeight + HeightOffset);
    }
}
