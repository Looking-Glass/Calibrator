using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace hypercube
{
    public class vertexCalibratorUndoManager 
    {

        public const uint undoCount = 25;

        List<Vector2[,,]> undoQueue = new List<Vector2[,,]>();

        int currentPosition;

        public void recordUndo(Vector2[,,] u)
        {
            //if we have made some undo's, the ones that we have undone should be removed before adding the new undo
            if (undoQueue.Count - (currentPosition + 1) > 0)
                undoQueue.RemoveRange(currentPosition + 1, undoQueue.Count - (currentPosition + 1) );

            undoQueue.Add(u);

            //get rid of too many undo's
            while (undoQueue.Count > undoCount)
                undoQueue.RemoveAt(0);

            currentPosition = undoQueue.Count - 1; //set our position to the most current
        }

        public Vector2[,,] undo()
        {
            currentPosition--;
            if (currentPosition < 0)
                currentPosition = 0;

            if (currentPosition >= undoQueue.Count || undoQueue.Count == 0)
                return null;
            
            return undoQueue[currentPosition];
        }

        public Vector2[,,] redo()
        {
            currentPosition++;
            if (currentPosition >= undoQueue.Count)
                currentPosition = undoQueue.Count - 1;

            if (currentPosition < 0 || undoQueue.Count == 0)
                return null;

            return undoQueue[currentPosition];
        }

    }
}
