using System;
using System.Linq;
using UnityEngine;
using Cars.Movement;

namespace Cars.OnScreen
{
    public class MovingRPMNeedle : MonoBehaviour
    {
        private ParentControl _pC;
        private OnScreenParent _osp;
        
        private const float START_POS = -13.617f;
        private const float LEER_GAS_POS = -47.677f;
        private const float END_POS = -254.978f;

        private float CorrectStartPos => _pC.IsTurnedOn ? LEER_GAS_POS : START_POS;

        private const int Z_NEEDLE_POS_ARR_SIZE = 241;
        private float[] _zNeedlePositions = new float[Z_NEEDLE_POS_ARR_SIZE];
        private int _zNeedlePosCounter;

        private int ZNeedlePosCounter
        {
            get => _zNeedlePosCounter;
            set
            {
                if (value >= Z_NEEDLE_POS_ARR_SIZE)
                {
                    _zNeedlePosCounter = 0;
                    return;
                }
                
                _zNeedlePosCounter = value;
            }
        }

        private const int SAMPLE_SPEED_ARR_SIZE = 500;
        private float[] _RPMSpeeds = new float[SAMPLE_SPEED_ARR_SIZE];
        private float[] _speeds = new float[SAMPLE_SPEED_ARR_SIZE];
        private int _counter;
        
        private int Counter
        {
            get => _counter;
            set
            {
                if (value >= SAMPLE_SPEED_ARR_SIZE)
                {
                    _counter = 0;
                    return;
                }
                
                _counter = value;
            }
        }
        
        private float _avgClampedSpeed;
        private float _avgZNeedlePosition;
        
        private void Start()
        {
            _osp = transform.parent.GetComponentInParent<OnScreenParent>();
            _pC = _osp.carObjectReference.GetComponent<ParentControl>();
            Array.Fill(_zNeedlePositions, START_POS);
        }

        private void Update()
        {
            PopulateSpeeds();
            UpdateAvgClampedSpeed();

            PopulateZNeedlePositions(FindZNeedlePosition());
            _avgZNeedlePosition = _zNeedlePositions.Average();
            
            _pC.RPMNeedle01Position = Mathf.InverseLerp(CorrectStartPos, END_POS, _avgZNeedlePosition);
            
            _osp.UpdateNeedlePosition(_avgZNeedlePosition, gameObject);
        }

        private float FindZNeedlePosition()
        {
            if (_pC.CurrentGear == ParentControl.GearsEnum.Neutral || _pC.Clutch)
                return ScaleNeedlePositionToScaledRadian(_pC.FindCorrectRadianEndpointToGear());

            return ScaleNeedlePositionToAvgSpeed();
        }

        private void PopulateZNeedlePositions(in float zNeedlePosition)
        {
            _zNeedlePositions[ZNeedlePosCounter++] = zNeedlePosition;
        }

        private void UpdateAvgClampedSpeed()
        {
            var avgSpeed = (_speeds.Average() + _RPMSpeeds.Average()) / 2;
            _avgClampedSpeed = Mathf.Clamp(avgSpeed, _pC.FindCorrectMinSpeedToGear(), _pC.FindCorrectMaxSpeedToGear());
        }

        private void PopulateSpeeds()
        {
            if (_pC.SpeedInKmhFromRPM == 0f)
                _RPMSpeeds[Counter] = _RPMSpeeds[Counter - 1 < 0 ? SAMPLE_SPEED_ARR_SIZE - 1 : Counter - 1] * 0.999f;
            else
                _RPMSpeeds[Counter] = _pC.SpeedInKmhFromRPM;
            
            _speeds[Counter++] = _pC.SpeedInKmh;
        }

        private float ScaleNeedlePositionToAvgSpeed()
        {
            var numerator = (END_POS - CorrectStartPos) * (_avgClampedSpeed - _pC.FindCorrectMinSpeedToGear());

            return numerator / (_pC.FindCorrectMaxSpeedToGear() - _pC.FindCorrectMinSpeedToGear()) + CorrectStartPos;
        }
        
        private float ScaleNeedlePositionToScaledRadian(in float scaledRadianEndpoint)
        {
            var numerator = (END_POS - CorrectStartPos) * _pC.Radian;
        
            return numerator / scaledRadianEndpoint + CorrectStartPos;
        }
    }
}
