using UnityEngine;
using System.Collections;

public enum CameraMode
{
    TeeShot,        // Behind player on tee
    Approach,       // Behind ball for approach shots
    Putting,        // Close view for putting
    BallFlight,     // Following ball in air
    Overview        // High overview of hole
}

public class GolfCameraController : MonoBehaviour
{
    [Header("═══ CAMERA TARGETS ═══")]
    [SerializeField] private Transform ballTransform;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform currentPin;
    
    [Header("═══ CAMERA POSITIONING ═══")]
    [Tooltip("Height to ensure 1.8m figure is visible")]
    [SerializeField] private float playerHeight = 1.8f;
    
    [Tooltip("Distance behind ball for standard shots")]
    [SerializeField] private float standardDistance = 5f;
    
    [Tooltip("Distance behind tee for tee shots")]
    [SerializeField] private float teeDistance = 8f;
    
    [Tooltip("Height above ground")]
    [SerializeField] private float cameraHeight = 2f;
    
    [Tooltip("Close distance for putting")]
    [SerializeField] private float puttingDistance = 3f;
    
    [Tooltip("Overview height for hole view")]
    [SerializeField] private float overviewHeight = 50f;
    
    [Header("═══ CAMERA MOVEMENT ═══")]
    [SerializeField] private float transitionSpeed = 2f;
    [SerializeField] private float rotationSpeed = 3f;
    [SerializeField] private bool smoothTransitions = true;
    
    [Header("═══ BALL TRACKING ═══")]
    [SerializeField] private bool followBallInFlight = true;
    [SerializeField] private float ballTrackingSpeed = 5f;
    [SerializeField] private float ballTrackingHeight = 10f;
    
    [Header("═══ AUTO POSITIONING ═══")]
    [SerializeField] private bool autoPositionOnBallStop = true;
    [SerializeField] private float ballStoppedThreshold = 0.5f;
    [SerializeField] private float repositionDelay = 1f;
    
    private Camera playerCamera;
    private GolfBallController ballController;
    private CameraMode currentMode = CameraMode.TeeShot;
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private bool isTransitioning = false;
    private Coroutine currentTransition;
    
    // Ball tracking variables
    private Vector3 lastBallPosition;
    private float ballStoppedTimer = 0f;
    private bool ballWasMoving = false;
    
    void Start()
    {
        playerCamera = GetComponent<Camera>();
        if (playerCamera == null)
            playerCamera = Camera.main;
            
        if (ballTransform != null)
        {
            ballController = ballTransform.GetComponent<GolfBallController>();
            lastBallPosition = ballTransform.position;
        }
        
        SetupInitialPosition();
    }
    
    void Update()
    {
        if (ballTransform == null) return;
        
        UpdateBallTracking();
        UpdateCameraMode();
        
        if (!isTransitioning && smoothTransitions)
        {
            SmoothCameraMovement();
        }
    }
    
    void UpdateBallTracking()
    {
        if (ballController == null) return;
        
        float ballSpeed = ballController.GetBallSpeed();
        Vector3 currentBallPos = ballTransform.position;
        
        // Check if ball is moving
        bool ballIsMoving = ballSpeed > ballStoppedThreshold;
        
        if (ballIsMoving)
        {
            ballWasMoving = true;
            ballStoppedTimer = 0f;
            
            // Switch to ball flight mode if ball is airborne and moving fast
            if (followBallInFlight && ballSpeed > 5f && !ballController.IsGrounded())
            {
                if (currentMode != CameraMode.BallFlight)
                {
                    SetCameraMode(CameraMode.BallFlight);
                }
            }
        }
        else if (ballWasMoving)
        {
            // Ball has stopped moving
            ballStoppedTimer += Time.deltaTime;
            
            if (ballStoppedTimer >= repositionDelay && autoPositionOnBallStop)
            {
                PositionForNextShot();
                ballWasMoving = false;
                ballStoppedTimer = 0f;
            }
        }
        
        lastBallPosition = currentBallPos;
    }
    
    void UpdateCameraMode()
    {
        switch (currentMode)
        {
            case CameraMode.BallFlight:
                TrackBallInFlight();
                break;
                
            case CameraMode.TeeShot:
            case CameraMode.Approach:
            case CameraMode.Putting:
                if (!isTransitioning)
                {
                    CalculateTargetPositionAndRotation();
                }
                break;
        }
    }
    
    void TrackBallInFlight()
    {
        if (ballTransform == null) return;
        
        Vector3 ballPos = ballTransform.position;
        Vector3 ballVelocity = ballController.GetBallVelocity();
        
        // Position camera behind and above the ball
        Vector3 behindBall = ballPos - ballVelocity.normalized * 10f;
        behindBall.y = ballPos.y + ballTrackingHeight;
        
        // Smooth camera movement
        targetPosition = behindBall;
        targetRotation = Quaternion.LookRotation((ballPos - behindBall).normalized);
    }
    
    void CalculateTargetPositionAndRotation()
    {
        if (ballTransform == null) return;
        
        Vector3 ballPos = ballTransform.position;
        Vector3 directionToPin = Vector3.zero;
        
        if (currentPin != null)
        {
            directionToPin = (currentPin.position - ballPos).normalized;
        }
        else
        {
            directionToPin = Vector3.forward; // Default direction
        }
        
        // Calculate camera position based on mode
        Vector3 cameraPos = Vector3.zero;
        float distance = standardDistance;
        
        switch (currentMode)
        {
            case CameraMode.TeeShot:
                distance = teeDistance;
                break;
                
            case CameraMode.Approach:
                distance = standardDistance;
                break;
                
            case CameraMode.Putting:
                distance = puttingDistance;
                break;
        }
        
        // Position camera behind ball, facing toward pin
        cameraPos = ballPos - directionToPin * distance;
        cameraPos.y = ballPos.y + cameraHeight;
        
        // Ensure 1.8m figure would be visible
        AdjustForPlayerVisibility(ref cameraPos, ballPos, directionToPin);
        
        targetPosition = cameraPos;
        targetRotation = Quaternion.LookRotation(directionToPin);
    }
    
    void AdjustForPlayerVisibility(ref Vector3 cameraPos, Vector3 ballPos, Vector3 facing)
    {
        // Calculate field of view requirements for 1.8m figure
        float fov = playerCamera.fieldOfView * Mathf.Deg2Rad;
        float requiredDistance = (playerHeight * 0.5f) / Mathf.Tan(fov * 0.5f);
        
        // Ensure minimum distance for player visibility
        float currentDistance = Vector3.Distance(cameraPos, ballPos);
        if (currentDistance < requiredDistance)
        {
            Vector3 directionFromBall = (cameraPos - ballPos).normalized;
            cameraPos = ballPos + directionFromBall * requiredDistance;
        }
        
        // Adjust height to ensure player's head is visible
        float minHeight = ballPos.y + (playerHeight * 0.1f); // 10% above player height
        if (cameraPos.y < minHeight)
        {
            cameraPos.y = minHeight;
        }
    }
    
    void SmoothCameraMovement()
    {
        if (Vector3.Distance(transform.position, targetPosition) > 0.1f)
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, transitionSpeed * Time.deltaTime);
        }
        
        if (Quaternion.Angle(transform.rotation, targetRotation) > 1f)
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }
    
    public void SetCameraMode(CameraMode mode)
    {
        if (currentMode == mode) return;
        
        currentMode = mode;
        
        if (smoothTransitions)
        {
            if (currentTransition != null)
                StopCoroutine(currentTransition);
                
            currentTransition = StartCoroutine(TransitionToNewMode());
        }
        else
        {
            CalculateTargetPositionAndRotation();
            transform.position = targetPosition;
            transform.rotation = targetRotation;
        }
        
        Debug.Log($"Camera mode changed to: {mode}");
    }
    
    IEnumerator TransitionToNewMode()
    {
        isTransitioning = true;
        
        CalculateTargetPositionAndRotation();
        
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        
        float transitionTime = 1f / transitionSpeed;
        float elapsed = 0f;
        
        while (elapsed < transitionTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / transitionTime;
            t = Mathf.SmoothStep(0f, 1f, t); // Smooth interpolation
            
            transform.position = Vector3.Lerp(startPos, targetPosition, t);
            transform.rotation = Quaternion.Lerp(startRot, targetRotation, t);
            
            yield return null;
        }
        
        transform.position = targetPosition;
        transform.rotation = targetRotation;
        
        isTransitioning = false;
    }
    
    public void PositionForTeeShot(Vector3 teePosition, Vector3 pinPosition)
    {
        if (ballTransform == null) return;
        
        // Update pin reference
        if (currentPin != null)
        {
            currentPin.position = pinPosition;
        }
        
        // Position ball at tee
        ballTransform.position = teePosition + Vector3.up * 0.1f;
        
        // Set camera mode
        SetCameraMode(CameraMode.TeeShot);
        
        Debug.Log("Positioned camera for tee shot");
    }
    
    public void PositionForNextShot()
    {
        if (ballTransform == null || currentPin == null) return;
        
        float distanceToPin = Vector3.Distance(ballTransform.position, currentPin.position);
        
        // Determine appropriate camera mode based on distance to pin
        if (distanceToPin < 30f) // Close to green
        {
            SetCameraMode(CameraMode.Putting);
        }
        else if (distanceToPin < 100f) // Approach shot
        {
            SetCameraMode(CameraMode.Approach);
        }
        else // Long shot
        {
            SetCameraMode(CameraMode.Approach);
        }
        
        Debug.Log($"Positioned camera for next shot. Distance to pin: {distanceToPin:F1}m");
    }
    
    public void ShowHoleOverview()
    {
        if (ballTransform == null || currentPin == null) return;
        
        SetCameraMode(CameraMode.Overview);
        
        // Position camera high above to show entire hole
        Vector3 ballPos = ballTransform.position;
        Vector3 pinPos = currentPin.position;
        Vector3 midPoint = (ballPos + pinPos) * 0.5f;
        
        targetPosition = midPoint + Vector3.up * overviewHeight;
        targetRotation = Quaternion.LookRotation(Vector3.down);
        
        if (!smoothTransitions)
        {
            transform.position = targetPosition;
            transform.rotation = targetRotation;
        }
        
        Debug.Log("Showing hole overview");
    }
    
    public void SetBallAndPin(Transform ball, Transform pin)
    {
        ballTransform = ball;
        currentPin = pin;
        
        if (ball != null)
        {
            ballController = ball.GetComponent<GolfBallController>();
            lastBallPosition = ball.position;
        }
    }
    
    public void SetPlayerTransform(Transform player)
    {
        playerTransform = player;
    }
    
    void SetupInitialPosition()
    {
        if (ballTransform != null && currentPin != null)
        {
            PositionForTeeShot(ballTransform.position, currentPin.position);
        }
    }
    
    // Public methods for external control
    public void ToggleOverview()
    {
        if (currentMode == CameraMode.Overview)
        {
            PositionForNextShot();
        }
        else
        {
            ShowHoleOverview();
        }
    }
    
    public CameraMode GetCurrentMode()
    {
        return currentMode;
    }
    
    public bool IsTransitioning()
    {
        return isTransitioning;
    }
    
    void OnDrawGizmos()
    {
        if (ballTransform == null) return;
        
        // Draw camera target position
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(targetPosition, 0.5f);
        
        // Draw line from camera to ball
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, ballTransform.position);
        
        // Draw player visibility cone
        if (currentMode != CameraMode.BallFlight && currentMode != CameraMode.Overview)
        {
            Gizmos.color = Color.blue;
            Vector3 ballPos = ballTransform.position;
            Vector3 cameraPos = transform.position;
            Vector3 direction = (ballPos - cameraPos).normalized;
            
            // Draw visibility cone for 1.8m figure
            float fov = playerCamera.fieldOfView * Mathf.Deg2Rad;
            float distance = Vector3.Distance(cameraPos, ballPos);
            float radius = distance * Mathf.Tan(fov * 0.5f);
            
            Gizmos.DrawWireSphere(ballPos + Vector3.up * (playerHeight * 0.5f), radius);
        }
    }
}