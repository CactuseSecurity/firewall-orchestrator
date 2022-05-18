namespace FWO.Ui.Services
{
    public class StateMatrix
    {
        private Dictionary<int, List<int>> matrix = new Dictionary<int, List<int>>();
        private Dictionary<int, int> derivedStates = new Dictionary<int, int>();
        public int LowestStartedState { get; set; }
        public int LowestEndState { get; set; }

        public void Init(int phase)
        {
            matrix = new Dictionary<int, List<int>>();
            derivedStates = new Dictionary<int, int>();
            switch(phase)
            {
                case 0:
                    matrix.Add(0, new List<int>(){0,9,62});
                    matrix.Add(9, new List<int>(){9,62});
                    matrix.Add(62, new List<int>(){62});
                    LowestStartedState = 0;
                    LowestEndState = 9;
                    break;
                case 2:
                    matrix.Add(9, new List<int>(){21});
                    matrix.Add(21, new List<int>(){21,22,23,29});
                    matrix.Add(22, new List<int>(){22,21,23,29});
                    matrix.Add(23, new List<int>(){23,21,22,29,61});
                    matrix.Add(29, new List<int>(){29});
                    matrix.Add(61, new List<int>(){61});
                    LowestStartedState = 21;
                    LowestEndState = 29;
                    derivedStates.Add(9, 9);
                    derivedStates.Add(21, 21);
                    derivedStates.Add(22, 21);
                    derivedStates.Add(23, 21);
                    derivedStates.Add(29, 29);
                    derivedStates.Add(61, 61);
                    break;
                default:
                    break;
            }
        }

        public List<int> getAllowedTransitions(int stateIn)
        {
            List<int> statesOut = new List<int>();
            if(matrix.ContainsKey(stateIn))
            {
                statesOut = matrix[stateIn];
            }
            return statesOut;
        }

        public int getRequestStateFromTaskStates(List<int> statesIn)
        {
            int stateOut = 0;
            int initState = 0;
            int inWorkState = LowestEndState;
            int maxState = 0;
            int openTasks = 0;
            int inWorkTasks = 0;
            int finishedTasks = 0;
            foreach(int state in statesIn)
            {
                if(state < LowestStartedState)
                {
                    openTasks++;
                    initState = state;
                }
                else if(state < LowestEndState)
                {
                    inWorkTasks++;
                    if(state < inWorkState)
                    {
                        inWorkState = state;
                    }
                }
                else
                {
                    finishedTasks++;
                    if(state > maxState)
                    {
                        maxState = state;
                    }
                }
            }
            if(inWorkTasks > 0)
            {
                stateOut = inWorkState;
            }
            else if(finishedTasks == statesIn.Count)
            {
                stateOut = maxState;
            }
            else if(openTasks == statesIn.Count)
            {
                stateOut = initState;
            }
            else
            {
                stateOut = LowestStartedState;
            }
            return derivedStates[stateOut];
        }
    }
}
