using UnityEngine;

namespace Unity.Template.CompetitiveActionMultiplayer
{
    public class PlayerSingle : MonoBehaviour
    {
        private Rigidbody2D rb;
        public Transform[] thrusters;
        public float speedPerThruster;
        public Transform[] turrets;
        public float speedPerTurret;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            rb = GetComponent<Rigidbody2D>();
        }

        // Update is called once per frame
        void Update()
        {
            //button held down
            if(Input.GetButton("Jump")){
                foreach (Transform thruster in thrusters)
                {
                    rb.AddForceAtPosition(transform.up * speedPerThruster, thruster.position);
                    //flame graphic enabled
                    thruster.GetChild(0).gameObject.SetActive(true);
                }
            }else if(Input.GetButtonUp("Jump")){
                foreach (Transform thruster in thrusters)
                {
                    //flame graphic disabled
                    thruster.GetChild(0).gameObject.SetActive(false);
                }
            }
            //button pressed
            if(Input.GetButtonDown("Fire1")){
                print("testing");
                foreach (Transform gun in turrets)
                {
                    rb.AddForceAtPosition(transform.up * -speedPerTurret, gun.position);
                }
            }
            rb.AddTorque(Input.GetAxis("Horizontal") * -0.1f * rb.totalForce.y, ForceMode2D.Force);
            
        }
    }
}
