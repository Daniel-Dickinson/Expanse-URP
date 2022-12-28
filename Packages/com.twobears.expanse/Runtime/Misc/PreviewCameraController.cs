using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace TwoBears.Expanse.Samples
{
    public class PreviewCameraController : MonoBehaviour
    {
        [Header("Rotation")]
        public float rotationSensitivity = 1.0f;
        public float rotationSmooth = 0.3f;

        [Header("Angle")]
        public float defaultAngle = 44f;
        public float minAngle = 35f;
        public float maxAngle = 90f;

        [Header("Rotation")]
        public float defaultRotation = 0;
        public float snapIncrement = 45;
        public float snapOffset = 0;

        [Header("Zoom")]
        public float defaultZoom = 14.0f;
        public float minZoom = 6.0f;
        public float maxZoom = 40.0f;
        public float zoomSpeed = 12.0f;
        public float zoomSmooth = 0.1f;
        public float zoomSnapIncrement = 2;

        [Header("ScreenSaver")]
        public float startTime = 10.0f;
        public float saverSpeed = 2.0f;

        [Header("Field of View")]
        public float fieldOfView = 110;
        public float minFOV = 80;
        public float maxFOV = 110;

        //Properties
        public Camera Camera
        {
            get { return controlledCamera; }
        }
        public float FieldOfView
        {
            set
            {
                if (controlledCamera == null)
                {
                    controlledCamera = GetComponentInChildren<Camera>();
                }
                fieldOfView = Mathf.Clamp(value, minFOV, maxFOV);
                controlledCamera.fieldOfView = HorizontalToVerticalFOV(fieldOfView, controlledCamera.aspect);
            }
        }

        public Vector3 Forward
        {
            get
            {
                return (Vector3.Cross(controlledCamera.transform.right, Vector3.up)).normalized;
            }
        }
        public Vector3 Right
        {
            get { return (Vector3.Cross(-Forward, Vector3.up)).normalized; }
        }

        public Quaternion Rotation
        {
            get { return Quaternion.LookRotation(transform.position - controlledCamera.transform.position); }
        }
        public Quaternion InverseRotation
        {
            get { return Quaternion.LookRotation(controlledCamera.transform.position - transform.position); }
        }
        public Quaternion FlattenedRotation
        {
            get
            {
                Vector3 direction = transform.position - controlledCamera.transform.position;
                direction.y = 0;
                return Quaternion.LookRotation(direction.normalized);
            }
        }

        //Backing fields
        protected Camera controlledCamera;

        //Zoom
        protected float zoom;
        private float zoomInput;
        private float zoomVelocity;

        //Overrides
        private bool overriding;
        private float angleOverride;
        private float zoomOverrideMin;
        private float zoomOverrideMax;

        private Vector3 cameraOffset;

        //Locks
        private bool xLock;
        private bool yLock;
        private bool zLock;

        private bool xDragLock;
        private bool yDragLock;

        //Angle
        private float cameraAngle;
        private float angleVelocity;

        //Rotation
        private float cameraRotation;
        private float rotationVelocity;

        //Input
        private float initialMouseX;
        private float currentMouseX;
        private float initialRotOffset;
        private float currentRotOffset;

        private float initialMouseY;
        private float currentMouseY;
        private float initialAngle;
        private float currentAngle;

        private bool validDrag;

        //Screensaver
        private float ssTime;

        //Generic methods
        protected virtual void Awake()
        {
            controlledCamera = GetComponentInChildren<Camera>();
        }
        protected virtual void Start()
        {
            //Set default zoom
            zoom = defaultZoom;
            zoomInput = zoom;

            //Set default angle
            cameraAngle = defaultAngle;
            currentAngle = cameraAngle;

            //Set default rotation
            currentRotOffset = defaultRotation;
            cameraRotation = currentRotOffset;

            //Set default field of view
            FieldOfView = fieldOfView;
        }

        protected void Update()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            RotationZoomInput();
            ScreensaverInput();
        }
        protected void LateUpdate()
        {
            ApplyRotationZoom();
        }

        //Rotation & zoom
        protected void RotationZoomInput()
        {
            //Zoom Input
            if (!zLock) zoomInput -= Input.GetAxis("Mouse ScrollWheel") * zoomSpeed;

            //Zoom override
            if (overriding) zoomInput = Mathf.Clamp(zoomInput, zoomOverrideMin, zoomOverrideMax);
            else zoomInput = Mathf.Clamp(zoomInput, minZoom, maxZoom);

            //Zoom snapping
            if (Input.GetKey(KeyCode.LeftControl))
            {
                zoomInput = Mathf.Round(zoomInput / zoomSnapIncrement) * zoomSnapIncrement;
            }

            //Zoom Smooth
            zoom = Mathf.SmoothDamp(zoom, zoomInput, ref zoomVelocity, zoomSmooth);

            //RotationInput
            if (Input.GetMouseButtonDown(0))
            {
                validDrag = true;

                if (xDragLock && !xLock) xDragLock = false;
                initialMouseX = Input.mousePosition.x;
                initialRotOffset = currentRotOffset;

                if (yDragLock && !yLock) yDragLock = false;
                initialMouseY = Input.mousePosition.y;
                initialAngle = currentAngle;
            }
        }
        protected void ApplyRotationZoom()
        {
            //Mouse rotation
            if (validDrag && Input.GetMouseButton(0))
            {
                if (!xDragLock)
                {
                    currentMouseX = Input.mousePosition.x;
                    currentRotOffset = initialRotOffset + ((currentMouseX - initialMouseX) * rotationSensitivity);

                    //Snapping
                    if (Input.GetKey(KeyCode.LeftControl))
                    {
                        float snappedOffset = currentRotOffset - snapOffset;

                        snappedOffset = Mathf.Round(snappedOffset / snapIncrement) * snapIncrement;

                        currentRotOffset = snappedOffset + snapOffset;
                    }
                }

                if (!yDragLock)
                {
                    currentMouseY = Input.mousePosition.y;
                    currentAngle = initialAngle - ((currentMouseY - initialMouseY) * rotationSensitivity);
                    currentAngle = Mathf.Clamp(currentAngle, minAngle, maxAngle);
                }
            }
            if (Input.GetMouseButtonUp(0)) validDrag = false;

            if (overriding) currentAngle = angleOverride;

            //Camera angle
            cameraAngle = Mathf.SmoothDampAngle(cameraAngle, currentAngle, ref angleVelocity, rotationSmooth);

            //Camera rotation
            cameraRotation = Mathf.SmoothDampAngle(cameraRotation, currentRotOffset, ref rotationVelocity, rotationSmooth);

            //Generate an offset from our zoom and angle
            float fieldfOfViewAdjust = Mathf.Pow(fieldOfView / maxFOV, -1);

            cameraOffset = Vector3.zero;
            cameraOffset.y = zoom * fieldfOfViewAdjust * (1.41f * Mathf.Sin(Mathf.Deg2Rad * cameraAngle));
            cameraOffset.z = -zoom * fieldfOfViewAdjust * (1.41f * Mathf.Cos(Mathf.Deg2Rad * cameraAngle));

            //Camera Position
            controlledCamera.transform.position = RotateAroundPoint(transform.position + cameraOffset, transform.position, Quaternion.Euler(new Vector3(0, cameraRotation, 0)));

            //Camera Rotation
            controlledCamera.transform.rotation = Quaternion.LookRotation(transform.position - controlledCamera.transform.position);
        }
        protected void ApplyCustomRotationZoom(float angle, float yRotation, float zoom)
        {
            //Camera angle
            if (float.IsNaN(cameraAngle)) cameraAngle = angle;
            cameraAngle = Mathf.SmoothDampAngle(cameraAngle, angle, ref angleVelocity, rotationSmooth);

            //Generate an offset from our zoom and angle
            float FielfOfViewAdjust = Mathf.Pow(fieldOfView / maxFOV, -1);

            cameraOffset = Vector3.zero;
            cameraOffset.y = zoom * FielfOfViewAdjust * (1.41f * Mathf.Sin(Mathf.Deg2Rad * cameraAngle));
            cameraOffset.z = -zoom * FielfOfViewAdjust * (1.41f * Mathf.Cos(Mathf.Deg2Rad * cameraAngle));

            //Camera Position
            Vector3 position = RotateAroundPoint(transform.position + cameraOffset, transform.position, Quaternion.Euler(new Vector3(0, yRotation, 0)));
            controlledCamera.transform.position = position;

            //Camera Rotation
            controlledCamera.transform.rotation = Quaternion.LookRotation(transform.position - controlledCamera.transform.position);
        }

        //Input lock
        public void LockInput(bool xAxis, bool yAxis, bool zoom)
        {
            if (xAxis)
            {
                xLock = true;
                xDragLock = true;
            }

            if (yAxis)
            {
                yLock = true;
                yDragLock = true;
            }

            if (zoom) zLock = true;
        }
        public void UnlockInput()
        {
            xLock = false;
            yLock = false;
            zLock = false;
        }

        //Rotation & zoom override
        public void LockRotationZoom(float angle, float zoomMin, float zoomMax)
        {
            overriding = true;
            angleOverride = angle;
            zoomOverrideMin = zoomMin;
            zoomOverrideMax = zoomMax;
        }
        public void ClearRotationZoom()
        {
            overriding = false;
        }

        //Screensaver
        private void ScreensaverInput()
        {
            if (Input.GetMouseButton(0) || overriding) ssTime = 0;
            else ssTime += Time.deltaTime;

            if (ssTime > startTime)
            {
                //Smooth angle to default
                currentAngle = Mathf.MoveTowards(currentAngle, defaultAngle, saverSpeed * Time.deltaTime);

                //Smooth zoom to default
                zoomInput = Mathf.MoveTowards(zoomInput, defaultZoom, saverSpeed * Time.deltaTime);

                //Pan slowly
                currentRotOffset += saverSpeed * Time.deltaTime;
            }
        }

        //Utility
        protected static float HorizontalToVerticalFOV(float horizontalFOV, float aspect)
        {
            return Mathf.Rad2Deg * 2 * Mathf.Atan(Mathf.Tan((horizontalFOV * Mathf.Deg2Rad) / 2f) / aspect);
        }
        protected Vector3 RotateAroundPoint(Vector3 point, Vector3 pivot, Quaternion Angle)
        {
            return Angle * (point - pivot) + pivot;
        }
    }
}