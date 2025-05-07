import unittest
import sys
import os

sys.path.append(os.path.join(os.path.dirname(__file__), '../importer'))

from importer.fwo_base import compute_min_moves


class TestFwoBase(unittest.TestCase):
    
    def test_compute_min_moves_on_insert(self):
        # arrange
        source_sequence = ["element a", "element b", "element c"]
        target_sequence = list(source_sequence)
        new_element = "element d"
        insert_position = 2
        target_sequence.insert(insert_position, new_element)
        expected_result = { 
            "moves": 1, 
            "operations": [f"Insert element '{new_element}' at target position {str(insert_position)}."],
            "insertions": [(insert_position, new_element)],
            "deletions": [],
            "reposition_moves": []
        }

        # act
        compute_min_moves_result = compute_min_moves(source_sequence, target_sequence)

        # assert
        self.assertEqual(compute_min_moves_result["moves"], expected_result["moves"])
        self.assertEqual(compute_min_moves_result["operations"], expected_result["operations"])
        self.assertEqual(compute_min_moves_result["insertions"], expected_result["insertions"])
        self.assertEqual(compute_min_moves_result["deletions"], expected_result["deletions"])     
        self.assertEqual(compute_min_moves_result["reposition_moves"], expected_result["reposition_moves"])      


    def test_compute_min_moves_on_delete(self):
        # arrange
        source_sequence = ["element a", "element c"]
        target_sequence = list(source_sequence)
        delete_position = 1
        deleted_element = target_sequence.pop(delete_position)
        expected_result = { 
            "moves": 1, 
            "operations": [f"Delete element '{deleted_element}' at source index {str(delete_position)}."],
            "insertions": [],
            "deletions": [(delete_position, deleted_element)],
            "reposition_moves": []
        }

        # act
        compute_min_moves_result = compute_min_moves(source_sequence, target_sequence)

        # assert
        self.assertEqual(compute_min_moves_result["moves"], expected_result["moves"])
        self.assertEqual(compute_min_moves_result["operations"], expected_result["operations"])
        self.assertEqual(compute_min_moves_result["insertions"], expected_result["insertions"])
        self.assertEqual(compute_min_moves_result["deletions"], expected_result["deletions"])     
        self.assertEqual(compute_min_moves_result["reposition_moves"], expected_result["reposition_moves"])


    def test_compute_min_moves_on_move(self):
        # arrange
        source_sequence = ["element a", "element b", "element c"]
        target_sequence = list(source_sequence)
        move_source_position = 2
        move_target_position = 1
        moved_element = target_sequence.pop(move_source_position)
        target_sequence.insert(move_target_position, moved_element)
        expected_result = { 
            "moves": 1, 
            "operations": [f"Pop element '{moved_element}' from source index {str(move_source_position)} and reinsert at target position {str(move_target_position)}."],
            "insertions": [],
            "deletions": [],
            "reposition_moves": [(move_source_position, moved_element, move_target_position)]
        }

        # act
        compute_min_moves_result = compute_min_moves(source_sequence, target_sequence)

        # assert
        self.assertEqual(compute_min_moves_result["moves"], expected_result["moves"])
        self.assertEqual(compute_min_moves_result["operations"], expected_result["operations"])
        self.assertEqual(compute_min_moves_result["insertions"], expected_result["insertions"])
        self.assertEqual(compute_min_moves_result["deletions"], expected_result["deletions"])     
        self.assertEqual(compute_min_moves_result["reposition_moves"], expected_result["reposition_moves"])
   
   