using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Накладывает горизонтальный градиент на Image с фото.
/// Поддерживает от 1 до 4 цветовых ячеек.
/// </summary>
[RequireComponent(typeof(Image))]
[AddComponentMenu("UI/Effects/Horizontal Gradient Overlay")]
public class HorizontalGradientOverlay : BaseMeshEffect
{
    [System.Serializable]
    public class GradientCell
    {
        [Tooltip("Цвет этой части градиента.")]
        public Color color = Color.white;

        [Range(0f, 1f)]
        [Tooltip("Позиция правой границы ячейки: 0 = левый край, 1 = правый край.")]
        public float endPosition = 1f;
    }

    [Header("Gradient")]
    [Range(1, 4)]
    [SerializeField] private int cellCount = 2;

    [SerializeField]
    private List<GradientCell> cells = new List<GradientCell>
    {
        new GradientCell { color = new Color(0f, 0f, 0f, 0.8f), endPosition = 0.5f },
        new GradientCell { color = new Color(0f, 0f, 0f, 0f),   endPosition = 1f }
    };

    [Header("Behavior")]
    [Tooltip("Если включено, цвета ячеек плавно переходят друг в друга.")]
    [SerializeField] private bool smoothBlend = true;

    [Tooltip("Если включено, цвет фото умножается на градиент. Иначе градиент полностью заменяет цвет.")]
    [SerializeField] private bool multiplyWithPhoto = false;

    private Image _image;

    [Tooltip("Ширина плавного перехода между ячейками. 0 = резкие границы.")]
    [Range(0f, 0.25f)]
    [SerializeField] private float blendWidth = 0.08f;

    protected override void Awake()
    {
        base.Awake();
        _image = GetComponent<Image>();
        _image.raycastTarget = false;
    }

    /// <summary>
    /// Применяет горизонтальный градиент из 4 цветов.
    /// Прозрачность всех цветов принудительно становится 100%.
    /// </summary>
    public void ApplyGradient(Color color1, Color color2, Color color3, Color color4)
    {
        color1.a = 1f;
        color2.a = 1f;
        color3.a = 1f;
        color4.a = 1f;

        cellCount = 4;

        EnsureCellCount(4);

        cells[0].color = color1;
        cells[1].color = color2;
        cells[2].color = color3;
        cells[3].color = color4;

        cells[0].endPosition = 0.25f;
        cells[1].endPosition = 0.50f;
        cells[2].endPosition = 0.75f;
        cells[3].endPosition = 1.00f;

        Refresh();
    }

    /// <summary>
    /// Применяет горизонтальный градиент из 3 цветов.
    /// Прозрачность всех цветов принудительно становится 100%.
    /// </summary>
    public void ApplyGradient(Color color1, Color color2, Color color3)
    {
        ApplyGradient(color1, color2, color3, color3);
    }

    /// <summary>
    /// Применяет горизонтальный градиент из 2 цветов.
    /// Прозрачность всех цветов принудительно становится 100%.
    /// </summary>
    public void ApplyGradient(Color color1, Color color2)
    {
        ApplyGradient(color1, color2, color2, color2);
    }

    public void SetCellCount(int count)
    {
        cellCount = Mathf.Clamp(count, 1, 4);
        NormalizeCells();
        Refresh();
    }

    public void SetCellColor(int index, Color color)
    {
        if (index < 0 || index >= cells.Count)
            return;

        cells[index].color = color;
        Refresh();
    }

    public void SetCellEndPosition(int index, float position)
    {
        if (index < 0 || index >= cells.Count)
            return;

        cells[index].endPosition = Mathf.Clamp01(position);
        NormalizeCells();
        Refresh();
    }

    public void Refresh()
    {
        if (_image != null)
            _image.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0)
            return;

        NormalizeCells();

        var vertices = new List<UIVertex>();
        vh.GetUIVertexStream(vertices);

        float minX = float.MaxValue;
        float maxX = float.MinValue;

        for (int i = 0; i < vertices.Count; i++)
        {
            float x = vertices[i].position.x;

            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
        }

        float width = maxX - minX;

        if (width <= 0.0001f)
            return;

        var result = new List<UIVertex>(vertices.Count);

        for (int i = 0; i < vertices.Count; i += 6)
        {
            int count = Mathf.Min(6, vertices.Count - i);
            var quad = new List<UIVertex>(vertices.GetRange(i, count));

            if (count == 6)
                SplitQuadByCells(quad, result, minX, width);
            else
                result.AddRange(quad);
        }

        vh.Clear();
        vh.AddUIVertexTriangleStream(result);
    }

    private void SplitQuadByCells(List<UIVertex> quad, List<UIVertex> output, float minX, float width)
    {
        float leftX = quad[0].position.x;
        float rightX = quad[2].position.x;

        float leftT = Mathf.Clamp01((leftX - minX) / width);
        float rightT = Mathf.Clamp01((rightX - minX) / width);

        var splitPositions = new List<float>();

        for (int i = 0; i < cells.Count - 1; i++)
        {
            float border = cells[i].endPosition;

            if (border > leftT + 0.0001f && border < rightT - 0.0001f)
                splitPositions.Add(border);
        }

        splitPositions.Sort();

        float segmentStart = leftT;
        Color startColor = GetColorAt(segmentStart);

        for (int i = 0; i <= splitPositions.Count; i++)
        {
            float segmentEnd = i < splitPositions.Count
                ? splitPositions[i]
                : rightT;

            Color endColor = GetColorAt(segmentEnd);

            AddGradientSegment(
                output,
                quad,
                minX,
                width,
                segmentStart,
                segmentEnd,
                startColor,
                endColor
            );

            segmentStart = segmentEnd;
            startColor = endColor;
        }
    }

    private void AddGradientSegment(
        List<UIVertex> output,
        List<UIVertex> sourceQuad,
        float minX,
        float width,
        float startT,
        float endT,
        Color startColor,
        Color endColor)
    {
        if (endT - startT <= 0.00001f)
            return;

        float startX = Mathf.Lerp(minX, minX + width, startT);
        float endX = Mathf.Lerp(minX, minX + width, endT);

        UIVertex bottomLeft = sourceQuad[0];
        UIVertex topLeft = sourceQuad[1];
        UIVertex topRight = sourceQuad[2];
        UIVertex bottomRight = sourceQuad[4];

        UIVertex a = bottomLeft;
        UIVertex b = topLeft;
        UIVertex c = topRight;
        UIVertex d = bottomRight;

        a.position = new Vector3(startX, bottomLeft.position.y, bottomLeft.position.z);
        b.position = new Vector3(startX, topLeft.position.y, topLeft.position.z);
        c.position = new Vector3(endX, topRight.position.y, topRight.position.z);
        d.position = new Vector3(endX, bottomRight.position.y, bottomRight.position.z);

        float uvLeft = Mathf.Lerp(sourceQuad[0].uv0.x, sourceQuad[2].uv0.x, startT);
        float uvRight = Mathf.Lerp(sourceQuad[0].uv0.x, sourceQuad[2].uv0.x, endT);

        a.uv0 = new Vector4(uvLeft, sourceQuad[0].uv0.y, sourceQuad[0].uv0.z, sourceQuad[0].uv0.w);
        b.uv0 = new Vector4(uvLeft, sourceQuad[1].uv0.y, sourceQuad[1].uv0.z, sourceQuad[1].uv0.w);
        c.uv0 = new Vector4(uvRight, sourceQuad[2].uv0.y, sourceQuad[2].uv0.z, sourceQuad[2].uv0.w);
        d.uv0 = new Vector4(uvRight, sourceQuad[4].uv0.y, sourceQuad[4].uv0.z, sourceQuad[4].uv0.w);

        a.color = ApplyBlend(sourceQuad[0].color, startColor);
        b.color = ApplyBlend(sourceQuad[1].color, startColor);
        c.color = ApplyBlend(sourceQuad[2].color, endColor);
        d.color = ApplyBlend(sourceQuad[4].color, endColor);

        output.Add(a);
        output.Add(b);
        output.Add(c);

        output.Add(a);
        output.Add(c);
        output.Add(d);
    }

    private Color ApplyBlend(Color photoColor, Color gradientColor)
    {
        return multiplyWithPhoto
            ? photoColor * gradientColor
            : gradientColor;
    }

    private Color GetColorAt(float normalizedX)
    {
        if (cells == null || cells.Count == 0)
            return Color.white;

        int count = cells.Count;

        if (count == 1)
            return cells[0].color;

        // До первой ячейки
        if (normalizedX <= 0f)
            return cells[0].color;

        // После последней ячейки
        if (normalizedX >= 1f)
            return cells[count - 1].color;

        if (!smoothBlend || blendWidth <= 0.0001f)
        {
            // Резкие цветные полосы
            for (int i = 0; i < count; i++)
            {
                if (normalizedX <= cells[i].endPosition)
                    return cells[i].color;
            }

            return cells[count - 1].color;
        }

        // Плавный переход только возле границ ячеек.
        // Внутри ячейки цвет остаётся чистым.
        for (int i = 0; i < count - 1; i++)
        {
            float border = cells[i].endPosition;
            float halfBlend = blendWidth * 0.5f;

            float blendStart = border - halfBlend;
            float blendEnd = border + halfBlend;

            if (normalizedX < blendStart)
            {
                return cells[i].color;
            }

            if (normalizedX <= blendEnd)
            {
                float t = Mathf.InverseLerp(blendStart, blendEnd, normalizedX);
                return Color.Lerp(cells[i].color, cells[i + 1].color, t);
            }
        }

        return cells[count - 1].color;
    }

    private void EnsureCellCount(int count)
    {
        if (cells == null)
            cells = new List<GradientCell>();

        while (cells.Count < count)
            cells.Add(new GradientCell());

        while (cells.Count > count)
            cells.RemoveAt(cells.Count - 1);
    }

    private void NormalizeCells()
    {
        if (cells == null)
            cells = new List<GradientCell>();

        while (cells.Count < cellCount)
            cells.Add(new GradientCell());

        while (cells.Count > cellCount)
            cells.RemoveAt(cells.Count - 1);

        cellCount = cells.Count;

        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i] == null)
                cells[i] = new GradientCell();

            cells[i].endPosition = Mathf.Clamp01(cells[i].endPosition);
        }

        for (int i = 1; i < cells.Count; i++)
        {
            if (cells[i].endPosition < cells[i - 1].endPosition)
                cells[i].endPosition = cells[i - 1].endPosition;
        }

        cells[cells.Count - 1].endPosition = 1f;
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();

        NormalizeCells();
        Refresh();
    }
#endif
}