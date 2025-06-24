using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Sprites;

[RequireComponent(typeof(RectTransform))]
public class SpeechBubble : MaskableGraphic
{
    [Tooltip("말풍선의 모서리 반경")]
    public float cornerRadius = 16f;

    [Tooltip("말꼬리의 너비")]
    public float tailWidth = 20f;

    [Tooltip("말꼬리의 높이")]
    public float tailHeight = 16f;

    [Tooltip("말꼬리 위치 (0~1: 왼쪽 끝~오른쪽 끝)")]
    [Range(0f, 1f)]
    public float tailPosition = 0.2f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = rectTransform.rect;
        float w = rect.width;
        float h = rect.height - tailHeight;
        float r = Mathf.Min(cornerRadius, Mathf.Min(w, h) * 0.5f);
        float tailX = w * tailPosition;

        // 코너 좌표 계산
        Vector3[] bodyVerts = new Vector3[8]
        {
        new Vector3(r, 0, 0),               // 0
        new Vector3(w - r, 0, 0),           // 1
        new Vector3(w, r, 0),               // 2
        new Vector3(w, h - r, 0),           // 3
        new Vector3(w - r, h, 0),           // 4
        new Vector3(r, h, 0),               // 5
        new Vector3(0, h - r, 0),           // 6
        new Vector3(0, r, 0)                // 7
        };

        // 말꼬리 버텍스
        Vector3 tailA = new Vector3(tailX, h, 0);                    // 8
        Vector3 tailB = new Vector3(tailX + tailWidth * 0.5f, h + tailHeight, 0); // 9
        Vector3 tailC = new Vector3(tailX + tailWidth, h, 0);        // 10

        // 버텍스 등록
        for (int i = 0; i < bodyVerts.Length; i++)
        {
            vh.AddVert(bodyVerts[i], color, Vector2.zero);
        }
        vh.AddVert(tailA, color, Vector2.zero);
        vh.AddVert(tailB, color, Vector2.zero);
        vh.AddVert(tailC, color, Vector2.zero);

        // 본체 삼각형(12개) – 안쪽 채우기
        int[] bodyTris = new int[]
        {
        0,1,7, 1,2,7, 2,3,7, 3,6,7,
        3,4,5, 3,5,6, 0,7,6, 0,6,5,
        0,5,4, 0,4,1, 1,4,3, 1,3,2
        };
        for (int i = 0; i < bodyTris.Length; i += 3)
        {
            vh.AddTriangle(bodyTris[i], bodyTris[i + 1], bodyTris[i + 2]);
        }

        // 말꼬리 삼각형
        vh.AddTriangle(7, 8, 10);   // 꼬리 왼쪽 연결
        vh.AddTriangle(8, 9, 10);   // 꼬리 몸체
    }

}
