using UnityEngine;
using System.Collections;

//a base class for an autocalibrator state

namespace hypercube
{
    public abstract class autoCalibratorModule
    {
        public abstract float getRelevantValue();

        public virtual void start(autoCalibrator a)
        { }

        public virtual void update(autoCalibrator a)
        { }

        public virtual void end(autoCalibrator a)
        { }
    }
}