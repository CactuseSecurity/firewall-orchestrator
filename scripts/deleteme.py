#!/usr/bin/python

import itertools

n = 3

while n < 11:

    n += 1
    
    myPermutations = list(itertools.permutations(range(n)))
    myRanking = []
    myRanking[0] = myPermutations[0]
    myTeamPool = list(itertools.permutations(range(n), 3))

    for myFirstRanking in myPermutations:
        myRanking[1] = myFirstRanking

        for mySecondRanking in myPermutations:
            myRanking[2] = mySecondRanking

            # pick team
            for myTeam in myTeamPool:

                for myPlayer in range(n):

                    if myPlayer in myTeam:
                        continue
                    else:
                        myPlayerScore = 0
                        for myTeamer in myTeam:
                            if myPlayer
            


