using UnityEngine;
using System.Collections;

namespace CharacterControllers {

    // TODO: Change this behaviour-wrapper to a modular character action system.
    public class PlatformerBehaviour : Platformer {

        public float gravity = -25f;
        public float runSpeed = 8f;
        public float groundDamping = 20f;
        public float inAirDamping = 5f;
        public float jumpHeight = 3f;
        public float jumpErrorAllowance = 0.1f;

        private float normalizedHorizontalSpeed = 0;
        private Animator animator;
        private RaycastHit2D lastControllerColliderHit;
        private Vector3 velocity;

        private bool wantsToJump;
        private float groundTime;
        private float jumpTime;

        public bool ShouldUpdate { get; set; }

        protected override void Awake() {
            base.Awake();

            ShouldUpdate = true;
            this.animator = GetComponent<Animator>();

            OnControllerCollided += OnControllerCollider;
            OnTriggerEnter += OnTriggerEnterEvent;
            OnTriggerExit += OnTriggerExitEvent;
        }

        private void OnControllerCollider(RaycastHit2D hit) {
            if (hit.normal.y == 1f)
                return;

            //Debug.Log( "flags: " + this.collisionState + ", hit.normal: " + hit.normal );
        }

        private void OnTriggerEnterEvent(Collider2D collider2D) {
            Debug.Log("OnTriggerEnterEvent: " + collider2D.gameObject.name);
        }

        private void OnTriggerExitEvent(Collider2D collider2D) {
            Debug.Log("OnTriggerExitEvent: " + collider2D.gameObject.name);
        }

        private void Update() {
            if (!ShouldUpdate)
                return;

            if (IsGrounded)
                this.velocity.y = 0;

            float horizontalInput = Input.GetAxisRaw("Horizontal");
            if (horizontalInput != 0) {
                this.normalizedHorizontalSpeed = Mathf.Sign(horizontalInput);
                if (Mathf.Sign(transform.localScale.x) != Mathf.Sign(horizontalInput)) {
                    Vector3 targetScale = transform.localScale;
                    targetScale.x *= -1;
                    transform.localScale = targetScale;
                }
                if (IsGrounded)
                    this.animator.Play(Animator.StringToHash("Run"));
            } else {
                this.normalizedHorizontalSpeed = 0;

                if (IsGrounded)
                    this.animator.Play(Animator.StringToHash("Idle"));
            }

            // Cache jump input to correct human error.
            if (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.W)) {
                this.jumpTime = this.jumpErrorAllowance;
            } else if (this.jumpTime >= 0) {
                this.jumpTime -= Time.deltaTime;
            }

            // Only allow jump whilst has been grounded recently.
            if (IsGrounded) { this.groundTime = 0; } else { this.groundTime += Time.deltaTime; }

            bool canJump = this.groundTime < this.jumpErrorAllowance && this.velocity.y <= 0;
            bool wantsToJump = this.jumpTime >= 0;

            if (canJump && wantsToJump) {
                this.velocity.y = Mathf.Sqrt(2f * this.jumpHeight * -this.gravity);
                this.animator.Play(Animator.StringToHash("Jump"));
            }

            // Apply horizontal smoothing.
            float smoothedMovementFactor = IsGrounded ? this.groundDamping : this.inAirDamping;
            float targetHorizontalVelocity = this.normalizedHorizontalSpeed * this.runSpeed;

            this.velocity.x = Mathf.Lerp(this.velocity.x, targetHorizontalVelocity, Time.deltaTime * smoothedMovementFactor);

            // Apply gravity.
            this.velocity.y += gravity * Time.deltaTime;

            // If holding down bump up our movement amount and turn off one way platform detection for a frame.
            // This allows for the character to jump down through one way platforms.
            if (IsGrounded && Input.GetAxisRaw("Vertical") < 0) {
                this.velocity.y *= 3f;
                this.ignoreOneWayPlatformsThisFrame = true;
            }

            Move(this.velocity * Time.deltaTime);

            this.animator.SetBool("IsFalling", !IsGrounded && this.velocity.y < 0);
        }
    }
}