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
        private float normalizedHorizontalSpeed = 0;

        private Animator animator;
        private RaycastHit2D lastControllerColliderHit;


        void Awake() {
            this.animator = GetComponent<Animator>();

            OnControllerCollided += onControllerCollider;
            OnTriggerEnter += onTriggerEnterEvent;
            OnTriggerExit += onTriggerExitEvent;
        }

        private void onControllerCollider(RaycastHit2D hit) {
            // bail out on plain old ground hits cause they arent very interesting
            if (hit.normal.y == 1f)
                return;

            // logs any collider hits if uncommented. it gets noisy so it is commented out for the demo
            //Debug.Log( "flags: " + this.collisionState + ", hit.normal: " + hit.normal );
        }


        private void onTriggerEnterEvent(Collider2D col) {
            Debug.Log("onTriggerEnterEvent: " + col.gameObject.name);
        }


        private void onTriggerExitEvent(Collider2D col) {
            Debug.Log("onTriggerExitEvent: " + col.gameObject.name);
        }



        // the Update loop contains a very simple example of moving the character around and controlling the animation
        private void Update() {
            if (IsGrounded)
                this.velocity.y = 0;

            if (Input.GetKey(KeyCode.RightArrow)) {
                normalizedHorizontalSpeed = 1;
                if (transform.localScale.x < 0f)
                    transform.localScale = new Vector3(-transform.localScale.x, transform.localScale.y,
                        transform.localScale.z);

                if (IsGrounded)
                    this.animator.Play(Animator.StringToHash("Run"));
            } else if (Input.GetKey(KeyCode.LeftArrow)) {
                normalizedHorizontalSpeed = -1;
                if (transform.localScale.x > 0f)
                    transform.localScale = new Vector3(-transform.localScale.x, transform.localScale.y,
                        transform.localScale.z);

                if (IsGrounded)
                    this.animator.Play(Animator.StringToHash("Run"));
            } else {
                normalizedHorizontalSpeed = 0;

                if (IsGrounded)
                    this.animator.Play(Animator.StringToHash("Idle"));
            }


            // we can only jump whilst grounded
            if (IsGrounded && Input.GetKeyDown(KeyCode.UpArrow)) {
                this.velocity.y = Mathf.Sqrt(2f * jumpHeight * -gravity);
                this.animator.Play(Animator.StringToHash("Jump"));
            }


            // Apply horizontal smoothing.
            float smoothedMovementFactor = IsGrounded ? groundDamping : inAirDamping;
            float targetHorizontalVelocity = this.normalizedHorizontalSpeed * this.runSpeed;

            Mathf.SmoothDamp(this.velocity.x, targetHorizontalVelocity, ref this.velocity.x, Time.deltaTime * smoothedMovementFactor);
            //this.velocity.x = Mathf.Lerp(this.velocity.x, normalizedHorizontalSpeed * runSpeed,
            //    Time.deltaTime * smoothedMovementFactor);

            // Apply gravity.
            this.velocity.y += gravity * Time.deltaTime;

            // If holding down bump up our movement amount and turn off one way platform detection for a frame.
            // This allows for the character to jump down through one way platforms.
            if (IsGrounded && Input.GetKey(KeyCode.DownArrow)) {
                this.velocity.y *= 3f;
                this.ignoreOneWayPlatformsThisFrame = true;
            }

            Move(this.velocity * Time.deltaTime);
        }
    }
}