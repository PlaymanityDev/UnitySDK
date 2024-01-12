using Adverty;
using Adverty.AdUnit;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(InMenuUnit))]
public class InMenuUnitClickHandler : MonoBehaviour, IPointerClickHandler
{
    private const string WRONG_COLLIDER_WARNING = "Click handler expects Collider on Unit.";

    private InMenuUnit inMenuUnit;
    private RectTransform rectTransform;
    private const float CENTER_POINT = 0.5f;
    private RenderType renderType;
    private Collider unitCollider;

    protected void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        inMenuUnit = GetComponent<InMenuUnit>();
        renderType = inMenuUnit.Configuration.RenderType;

        switch(renderType)
        {
            case RenderType.Sprite:
            case RenderType.Mesh:
                unitCollider = GetComponent<Collider>();
                break;
            default:
                break;
        }

        if(IsColliderWrong)
        {
            Debug.LogWarning(WRONG_COLLIDER_WARNING);
        }
    }

    protected void OnMouseDown()
    {
        if(renderType == RenderType.UI || IsColliderWrong)
        {
            return;
        }

        Vector2 normalizedPosition = new Vector2(CENTER_POINT, CENTER_POINT);

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if(!unitCollider.Raycast(ray, out hit, 100))
        {
            return;
        }

        Vector3 localPoint = unitCollider.transform.InverseTransformPoint(hit.point);

        switch(renderType)
        {
            case RenderType.Mesh:
                normalizedPosition.x = localPoint.x + CENTER_POINT;
                normalizedPosition.y = 1.0f - (localPoint.y + CENTER_POINT);
                break;
            case RenderType.Sprite:
                Vector3 colliderSize = (unitCollider as BoxCollider).size;
                normalizedPosition.x = ((localPoint.x + colliderSize.x) / colliderSize.x) - CENTER_POINT;
                normalizedPosition.y = 1.0f - ((localPoint.y + colliderSize.y) / colliderSize.y) + CENTER_POINT;
                break;
            default:
                return;
        }

        inMenuUnit.Interact(normalizedPosition);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if(renderType != RenderType.UI)
        {
            return;
        }

        Vector2 localPos;
        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out localPos))
        {
            return;
        }
        Vector2 normalizedPosition = GeneratePositionPoint(localPos);
        inMenuUnit.Interact(normalizedPosition);
    }

    private Vector2 GeneratePositionPoint(Vector2 localPoint)
    {
        Vector2 size = rectTransform.sizeDelta;
        float x = ((localPoint.x + (size.x * CENTER_POINT)) / size.x) - (CENTER_POINT - rectTransform.pivot.x);
        float y = 1.0f - (((localPoint.y + (size.y * CENTER_POINT)) / size.y) - (CENTER_POINT - rectTransform.pivot.y));
        return new Vector2(x, y);
    }

    private bool IsColliderWrong
    {
        get
        {
            return renderType == RenderType.Sprite && !(unitCollider is BoxCollider);
        }
    }
}
