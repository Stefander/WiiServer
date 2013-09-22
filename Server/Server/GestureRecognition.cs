using System;
using System.Collections.Generic;
using System.Text;

namespace WiiServer
{
    class GestureRecognition
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public GestureRecognition()
        {
            // Do whatever here to setup the gesture recognition :)
        }

        /// <summary>
        /// Gets the captured dump from the server, returns the gesture ID if there was a match
        /// </summary>
        /// <param name="list">List with captured points</param>
        /// <param name="gestures">List of gestures to match the points against</param>
        /// <returns>Returns gesture ID when a match was found, returns -1 if no match was found</returns>
        public int MatchGesture(List<Vector3> list,List<int> gestures,float gestureDuration)
        {
            Console.WriteLine(list.Count + " points to be matched against " + gestures.Count + " gestures (gesture time: "+gestureDuration+"s)");
            Console.WriteLine("Point 0: " + list[0].x + " " + list[0].y + " " + list[0].z);
            return 3;
        }
    }
}
