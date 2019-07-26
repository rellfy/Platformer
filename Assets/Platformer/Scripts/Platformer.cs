using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CharacterControllers {

    internal struct CharacterRaycastOrigins {
        public Vector3 topLeft;
        public Vector3 bottomRight;
        public Vector3 bottomLeft;
    }

    public class CharacterCollisionState2D {
        public bool above;
        public bool below;
        public bool left;
        public bool right;
        public bool movingDownSlope;
        public bool wasGroundedLastFrame;
        public bool becameGroundedThisFrame;
        public float slopeAngle;

        public bool IsColliding => this.below || this.right || this.left || this.above;

        public override string ToString() => $"r={this.right}, "+
            $"l={this.left}, a={this.above}, b={this.below}, movingDownSlope={this.movingDownSlope}, " +
            $"angle={this.slopeAngle}, wasGroundedLastFrame={this.wasGroundedLastFrame}, " +
            $"becameGroundedThisFrame={this.becameGroundedThisFrame}";

        public void Reset() {
            this.right =
                this.left = 
                this.above = 
                this.below =
                this.becameGroundedThisFrame =
                this.movingDownSlope = false;

            this.slopeAngle = 0f;
        }
    }

    [RequireComponent(typeof(BoxCollider2D), typeof(Rigidbody2D))]
    public class Platformer : MonoBehaviour {

        private const float krayOffsetFloatFudgeFactor = 0.001f;
        private readonly float slopeLimitTangent = Mathf.Tan(75f * Mathf.Deg2Rad);
        private float horizontalDistanceBetweenRays;
        private bool isGoingUpSlope;
        [SerializeField]
        [Range(0.001f, 0.3f)]
        private float rayOffset = 0.02f;
        private float verticalDistanceBetweenRays;
        private readonly List<RaycastHit2D> frameRaycastCache = new List<RaycastHit2D>(2);
        [SerializeField]
        private readonly LayerMask oneWayPlatformMask = 0;
        private RaycastHit2D raycastHit;
        private CharacterRaycastOrigins raycastOrigins;

        public bool ignoreOneWayPlatformsThisFrame;
        public float jumpingThreshold = 0.07f;
        public LayerMask platformMask = 0;
        [Range(0f, 90f)]
        public float slopeLimit = 30f;
        [Range(2, 20)]
        public int totalHorizontalRays = 8;
        [Range(2, 20)]
        public int totalVerticalRays = 4;
        [HideInInspector]
        [NonSerialized]
        public BoxCollider2D boxCollider2D;
        [HideInInspector]
        [NonSerialized]
        public CharacterCollisionState2D collisionState = new CharacterCollisionState2D();
        [HideInInspector]
        [NonSerialized]
        public Rigidbody2D rigidBody2D;
        public AnimationCurve slopeSpeedMultiplier =
            new AnimationCurve(
                new Keyframe(-90f, 1.5f),
                new Keyframe(0f, 1f),
                new Keyframe(90f, 0f));
        public LayerMask layerTriggerMask = 0;
        [HideInInspector]
        [NonSerialized]
        public Vector3 velocity;

        /// <summary>
        /// Defines the collider's offset for raycasts.
        /// </summary>
        public float RayOffset {
            get => this.rayOffset;
            set {
                this.rayOffset = value;
                RecalculateDistanceBetweenRays();
            }
        }

        public bool IsGrounded => this.collisionState.below;

        public event Action<RaycastHit2D> OnControllerCollided;
        public event Action<Collider2D> OnTriggerEnter;
        public event Action<Collider2D> OnTriggerStay;
        public event Action<Collider2D> OnTriggerExit;

        [Conditional("DEBUG_CC2D_RAYS")]
        private void DrawRay(Vector3 start, Vector3 dir, Color color) {
            Debug.DrawRay(start, dir, color);
        }

        public void Move(Vector3 deltaMovement) {
            this.collisionState.wasGroundedLastFrame = this.collisionState.below;

            // Clear current state.
            this.collisionState.Reset();
            this.frameRaycastCache.Clear();
            this.isGoingUpSlope = false;

            PrimeRaycastOrigins();

            // Check if the character is on a slope.
            if (deltaMovement.y < 0f && this.collisionState.wasGroundedLastFrame)
                HandleVerticalSlope(ref deltaMovement);

            if (deltaMovement.x != 0f)
                MoveAxis(true, ref deltaMovement);

            if (deltaMovement.y != 0f)
                MoveAxis(false, ref deltaMovement);

            // Update current state.
            deltaMovement.z = 0;
            transform.Translate(deltaMovement, Space.World);

            // Calculate velocity.
            if (Time.deltaTime > 0f)
                this.velocity = deltaMovement / Time.deltaTime;

            // Update the becameGrounded value of the current state.
            if (!this.collisionState.wasGroundedLastFrame && this.collisionState.below)
                this.collisionState.becameGroundedThisFrame = true;

            // Reset vertical velocity in case the character went up a slope.
            if (this.isGoingUpSlope)
                this.velocity.y = 0;

            // Trigger events.
            if (OnControllerCollided != null)
                this.frameRaycastCache.ForEach(x => OnControllerCollided(x));

            this.ignoreOneWayPlatformsThisFrame = false;
        }

        /// <summary>
        ///     moves directly down until grounded
        /// </summary>
        public void WarpToGrounded() {
            do {
                Move(new Vector3(0, -1f, 0));
            } while (!IsGrounded);
        }

        /// <summary>
        ///     Recalculates ray offsets. Should be called whenever the BoxCollider2D changes.
        /// </summary>
        public void RecalculateDistanceBetweenRays() {
            // Horizontal
            float colliderUseableHeight =
                this.boxCollider2D.size.y * Mathf.Abs(transform.localScale.y) - 2f * this.rayOffset;
            this.verticalDistanceBetweenRays = colliderUseableHeight / (this.totalHorizontalRays - 1);

            // Vertical
            float colliderUseableWidth =
                this.boxCollider2D.size.x * Mathf.Abs(transform.localScale.x) - 2f * this.rayOffset;
            this.horizontalDistanceBetweenRays = colliderUseableWidth / (this.totalVerticalRays - 1);
        }

        private void Awake() {
            this.boxCollider2D = GetComponent<BoxCollider2D>();
            this.rigidBody2D = GetComponent<Rigidbody2D>();

            // Add one-way platforms to normal platform mask that the character can land on them from above.
            this.platformMask |= this.oneWayPlatformMask;

            RayOffset = this.rayOffset;

            for (int i = 0; i < 32; i++) {
                // Ignore collisions outside of the layerTriggerMask
                if ((this.layerTriggerMask.value & (1 << i)) == 0)
                    Physics2D.IgnoreLayerCollision(gameObject.layer, i);
            }
        }

        public void OnTriggerEnter2D(Collider2D collider2D) {
            if (OnTriggerEnter != null)
                OnTriggerEnter(collider2D);
        }

        public void OnTriggerStay2D(Collider2D collider2D) {
            if (OnTriggerStay != null)
                OnTriggerStay(collider2D);
        }

        public void OnTriggerExit2D(Collider2D collider2D) {
            if (OnTriggerExit != null)
                OnTriggerExit(collider2D);
        }

        /// <summary>
        ///     Resets the raycastOrigins to the current extents of the box collider inset by the rayOffset. It is inset
        ///     to avoid casting a ray from a position directly touching another collider which results in wonky normal data.
        /// </summary>
        /// <param name="futurePosition">Future position.</param>
        /// <param name="deltaMovement">Delta movement.</param>
        private void PrimeRaycastOrigins() {
            Bounds modifiedBounds = this.boxCollider2D.bounds;
            modifiedBounds.Expand(-2f * this.rayOffset);

            this.raycastOrigins.topLeft = new Vector2(modifiedBounds.min.x, modifiedBounds.max.y);
            this.raycastOrigins.bottomRight = new Vector2(modifiedBounds.max.x, modifiedBounds.min.y);
            this.raycastOrigins.bottomLeft = modifiedBounds.min;
        }

        /// <summary>
        /// Move the character on an axis.
        /// </summary>
        /// <param name="x">Move character on the X axis. If false, moves on the Y axis.</param>
        /// <param name="deltaMovement">This character's current delta movement vector.</param>
        private void MoveAxis(bool x, ref Vector3 deltaMovement) {
            float deltaAxis = x ? deltaMovement.x : deltaMovement.y;

            void SetDeltaAxis(ref Vector3 dm) {
                if (x) {
                    dm.x = deltaAxis;
                } else {
                    dm.y = deltaAxis;
                }
            }
            
            // "Positive movement" corresponds to to right for X and up for Y.
            bool isMovingPositively = x ? deltaMovement.x > 0 : deltaMovement.y > 0;
            float rayDistance = Mathf.Abs(deltaAxis) + this.rayOffset;

            Vector3 rayDirection = isMovingPositively ?
                x ? Vector3.right : Vector3.up :
                x ? Vector3.left : Vector3.down;

            Vector2 initialRayOrigin = isMovingPositively ?
                x ? this.raycastOrigins.bottomRight : this.raycastOrigins.topLeft :
                this.raycastOrigins.bottomLeft;

            if (!x) {
                // Y movement gets called after X; Hence, account for the delta movement.
                initialRayOrigin.x += deltaMovement.x;

                // Do not collide with the oneWayPlatformMask layer when moving upwards.
                LayerMask mask = this.platformMask;
                if (isMovingPositively && !this.collisionState.wasGroundedLastFrame || this.ignoreOneWayPlatformsThisFrame)
                    mask &= ~this.oneWayPlatformMask;
            }

            for (int i = 0; i < (x ? this.totalHorizontalRays : this.totalVerticalRays); i++) {
                Vector2 ray = new Vector2(initialRayOrigin.x, initialRayOrigin.y + i * this.verticalDistanceBetweenRays);

                DrawRay(ray, rayDirection * rayDistance, Color.red);

                // TODO Fix this if it doesn't work.
                if (i == 0 && this.collisionState.wasGroundedLastFrame) {
                    this.raycastHit = Physics2D.Raycast(ray, rayDirection, rayDistance, this.platformMask);
                } else {
                    this.raycastHit = Physics2D.Raycast(ray, rayDirection, rayDistance, this.platformMask & ~this.oneWayPlatformMask);
                }

                // Let the bottom ray hit slopes.
                if (x && i == 0 && HandleHorizontalSlope(ref deltaMovement,
                    Vector2.Angle(this.raycastHit.normal, Vector2.up))) {
                    this.frameRaycastCache.Add(this.raycastHit);

                    // If not grounded from the last frame, land on the slope.
                    if (!this.collisionState.wasGroundedLastFrame) {
                        float flushDistance = Mathf.Sign(deltaMovement.x) * (this.raycastHit.distance - RayOffset);
                        transform.Translate(new Vector2(flushDistance, 0));
                    }

                    break;
                }

                deltaAxis = x ? this.raycastHit.point.x - ray.x : this.raycastHit.point.y - ray.y;
                rayDistance = Mathf.Abs(deltaAxis);
                deltaAxis += isMovingPositively ? -this.rayOffset : this.rayOffset;

                if (x && isMovingPositively) {
                    this.collisionState.right = true;
                } else if (x) {
                    this.collisionState.left = true;
                } else if (isMovingPositively) {
                    this.collisionState.above = true;
                } else {
                    this.collisionState.below = true;
                }

                this.frameRaycastCache.Add(this.raycastHit);

                // Ensure the character remains grounded (Y) when on a slope's edge.
                if (!x && !isMovingPositively && deltaAxis > 0.00001f)
                    this.isGoingUpSlope = true;

                if (rayDistance < this.rayOffset + krayOffsetFloatFudgeFactor)
                    break;
            }
            
            SetDeltaAxis(ref deltaMovement);
        }

        private void HandleVerticalSlope(ref Vector3 deltaMovement) {
            // Slope check from the center of the collider.
            float centerOfCollider = (this.raycastOrigins.bottomLeft.x + this.raycastOrigins.bottomRight.x) * 0.5f;
            Vector2 rayDirection = -Vector2.up;

            // The ray distance is based on the slopeLimit.
            float slopeCheckRayDistance =
                this.slopeLimitTangent * (this.raycastOrigins.bottomRight.x - centerOfCollider);

            Vector2 slopeRay = new Vector2(centerOfCollider, this.raycastOrigins.bottomLeft.y);
            DrawRay(slopeRay, rayDirection * slopeCheckRayDistance, Color.yellow);
            this.raycastHit = Physics2D.Raycast(slopeRay, rayDirection, slopeCheckRayDistance, this.platformMask);

            if (!this.raycastHit)
                return;

            float angle = Vector2.Angle(this.raycastHit.normal, Vector2.up);
            if (angle < 0.00001)
                return;

            bool isMovingDownSlope = Mathf.Sign(this.raycastHit.normal.x) == Mathf.Sign(deltaMovement.x);
            if (isMovingDownSlope) {
                // Speed up according to the multiplier.
                float slopeModifier = this.slopeSpeedMultiplier.Evaluate(-angle);
                // Add extra gravity to ensure character remains grounded.
                deltaMovement.y += this.raycastHit.point.y - slopeRay.y - RayOffset;
                deltaMovement.x *= slopeModifier;
                this.collisionState.movingDownSlope = true;
                this.collisionState.slopeAngle = angle;
            }
        }

        private bool HandleHorizontalSlope(ref Vector3 deltaMovement, float angle) {
            if (Mathf.RoundToInt(angle) == 90)
                return false;

            if (angle > this.slopeLimit) {
                deltaMovement.x = 0;
                return true;
            }

            // Adjust delta movement if not jumping only.
            if (deltaMovement.y >= this.jumpingThreshold)
                return true;

            // Slow movement according to the slope's speed multiplier.
            float slopeModifier = this.slopeSpeedMultiplier.Evaluate(angle);
            deltaMovement.x *= slopeModifier;

            // Do not set collisions on the sides for this since a slope is not considered a perpendicular collision.
            // Smooth y movement when climbing. Make the Y movement equivalent to the actual Y location that corresponds
            // to the new X location.
            deltaMovement.y = Mathf.Abs(Mathf.Tan(angle * Mathf.Deg2Rad) * deltaMovement.x);
            bool isGoingRight = deltaMovement.x > 0;

            // Safety check. Fire a ray in the direction of movement just in case the diagonal calculated above
            // goes through a wall. If the ray hits, we back off the horizontal movement to stay in bounds.
            Vector3 ray = isGoingRight ? this.raycastOrigins.bottomRight : this.raycastOrigins.bottomLeft;
            RaycastHit2D raycastHit;

            if (this.collisionState.wasGroundedLastFrame) {
                raycastHit = Physics2D.Raycast(ray, deltaMovement.normalized,
                    deltaMovement.magnitude, this.platformMask);
            } else {
                raycastHit = Physics2D.Raycast(ray, deltaMovement.normalized, deltaMovement.magnitude,
                    this.platformMask & ~this.oneWayPlatformMask);
            }

            if (raycastHit) {
                deltaMovement = (Vector3)raycastHit.point - ray;
                deltaMovement.x += isGoingRight ? -this.rayOffset : this.rayOffset;
            }

            this.isGoingUpSlope = true;
            this.collisionState.below = true;

            return true;
        }
    }
}