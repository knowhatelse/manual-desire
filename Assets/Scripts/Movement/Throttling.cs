using Models;
using System.Linq;
using UnityEngine;

namespace Movement
{
    public class Throttling : MonoBehaviour
    {
        private ParentControl _pC;
        private Rigidbody _rB;

        [field: SerializeField] private float MotorTorque { get; set; }

        private void Awake()
        {
            _pC = GetComponent<ParentControl>();
            _rB = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            RegulateHeat();
            
            if (!_pC.IsTurnedOn || _pC._brake)
            {
                ReleaseThrottle(_pC.FindCorrectRadianEndpointToGear());
                return;
            }

            if (!_pC._throttle && _pC.Clutch) return;
            
            if (_pC.Clutch && _pC._throttle)
            {
                IncreaseRadianInNeutral();
                return;
            }
            
            if (_pC._throttle) PressThrottle();
            else DecideWhenNoThrottle();
        }

        private void RegulateHeat()
        {
            float multiplier = _pC.Heat > 50f ? 0.75f : 1f;
            bool shouldCoolDownOnModerateRPMs = _pC.Heat > 50f;

            if (_pC.Radian < _pC.FindCorrectRadianEndpointToGear() * 0.65f)
            {
                if (shouldCoolDownOnModerateRPMs)
                {
                    _pC.Heat -= 0.02f * multiplier;
                }
                else
                {
                    _pC.Heat += 0.02f * multiplier;
                }
            }
            else if (_pC.Radian < _pC.FindCorrectRadianEndpointToGear() * 0.75f)
            {
                _pC.Heat += 0.05f * multiplier;
            }
            else
            {
                _pC.Heat += 0.1f * multiplier;
            }
        }

        private void PressThrottle()
        {
            if (_pC.CurrentGear == ParentControl.GearsEnum.NEUTRAL)
            {
                IncreaseRadianInNeutral();
                return;
            }
            if (_pC.CurrentGear == ParentControl.GearsEnum.REVERSE)
            {
                Gear firstGear = _pC.Car.Gears.Find(g => g.Level == 1);
                AdjustForReverse(firstGear);
            }
            else
            {
                float speedInKmh = _rB.velocity.magnitude * 3.6f;

                Gear gear = _pC.Car.Gears.Find(g => g.Level == (int)_pC.CurrentGear);
                AdjustThrottleToGear(speedInKmh, gear);
            }
            
            _pC.ApplyTorqueToWheels(MotorTorque);
        }

        private void IncreaseRadianInNeutral()
        {
            float smallestRadianEndpointOfAlLGears = _pC.Car.Gears.Min(g => g.ScaledRadianEndpoint);

            if (_pC.Radian < smallestRadianEndpointOfAlLGears)
            {
                const float step = Mathf.PI / 800;

                _pC.Radian += step;
            }
        }

        private void AdjustForReverse(Gear firstGear)
        {
            if (_pC.Radian < firstGear.ScaledRadianEndpoint)
            {
                const float thousandthOfRadian = Mathf.PI / 1000;

                _pC.Radian += thousandthOfRadian;
            }

            MotorTorque = (firstGear.LowestTorque + firstGear.DeltaTorque * Mathf.Sin(_pC.Radian * firstGear.RadianScalar)) * -1;
        }

        private void AdjustThrottleToGear(in float speedInKmh, Gear gear)
        {
            float scaledRadian = _pC.Radian * gear.RadianScalar;

            if (Mathf.Sin(scaledRadian) > 0.9999f)
            {
                Debug.Log($"({gear.Level}. gear) {speedInKmh * 3.6f} km/h | PEAK");
            }
            
            if (_pC.Radian < gear.ScaledRadianEndpoint)
            {
                const float thousandthOfRadian = Mathf.PI / 1000;

                _pC.Radian += thousandthOfRadian;
            }

            MotorTorque = gear.LowestTorque + gear.DeltaTorque * Mathf.Sin(scaledRadian);
            
            if (speedInKmh > gear.MaxSpeedKmh)
            {
                // the faster car goes above its gear limit, the smaller torque is applied (partly) proportionally
                float speedSubtractionStep = Mathf.Ceil(Mathf.Abs(speedInKmh - gear.MaxSpeedKmh)) / 100;
                
                MotorTorque *= Mathf.Clamp(0.45f - speedSubtractionStep, 0.2f, 0.70f);
            }
        }

        private void DecideWhenNoThrottle()
        {
            if (_pC.Radian < 0.001f)
            {
                if (_pC.CurrentGear == ParentControl.GearsEnum.FIRST)
                {
                    _pC.ApplyTorqueToWheels(10f);
                }
                else if (_pC.CurrentGear == ParentControl.GearsEnum.REVERSE)
                {
                    _pC.ApplyTorqueToWheels(-10f);
                }
                else if (_pC.CurrentGear != ParentControl.GearsEnum.NEUTRAL)
                {
                    JerkForward();
                    _pC.IsTurnedOn = false;
                }
            }
            else
            {
                ReleaseThrottle(_pC.FindCorrectRadianEndpointToGear());
            }
        }

        private void JerkForward()
        { 
            _rB.AddRelativeForce(Vector3.forward * 100, ForceMode.Acceleration);
        }

        private void ReleaseThrottle(in float scaledRadianEndpoint)
        {
            float dropRate = scaledRadianEndpoint * 0.0005f;
            
            _pC.Radian -= dropRate;
            
            MotorTorque = 0f;
            _pC.ApplyTorqueToWheels(MotorTorque);
        }

    }
}
