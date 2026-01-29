from fwo_base import compute_min_moves


def test_compute_min_moves_on_insert():
    # arrange
    source_sequence = ["element a", "element b", "element c"]
    target_sequence = list(source_sequence)
    new_element = "element d"
    insert_position = 2
    target_sequence.insert(insert_position, new_element)
    expected_result = {
        "moves": 1,
        "operations": [f"Insert element '{new_element}' at target position {insert_position!s}."],
        "insertions": [(insert_position, new_element)],
        "deletions": [],
        "reposition_moves": [],
    }

    # act
    compute_min_moves_result = compute_min_moves(source_sequence, target_sequence)

    # assert
    assert compute_min_moves_result["moves"] == expected_result["moves"]
    assert compute_min_moves_result["operations"] == expected_result["operations"]
    assert compute_min_moves_result["insertions"] == expected_result["insertions"]
    assert compute_min_moves_result["deletions"] == expected_result["deletions"]
    assert compute_min_moves_result["reposition_moves"] == expected_result["reposition_moves"]


def test_compute_min_moves_on_delete():
    # arrange
    source_sequence = ["element a", "element c"]
    target_sequence = list(source_sequence)
    delete_position = 1
    deleted_element = target_sequence.pop(delete_position)
    expected_result = {
        "moves": 1,
        "operations": [f"Delete element '{deleted_element}' at source index {delete_position!s}."],
        "insertions": [],
        "deletions": [(delete_position, deleted_element)],
        "reposition_moves": [],
    }

    # act
    compute_min_moves_result = compute_min_moves(source_sequence, target_sequence)

    # assert
    assert compute_min_moves_result["moves"] == expected_result["moves"]
    assert compute_min_moves_result["operations"] == expected_result["operations"]
    assert compute_min_moves_result["insertions"] == expected_result["insertions"]
    assert compute_min_moves_result["deletions"] == expected_result["deletions"]
    assert compute_min_moves_result["reposition_moves"] == expected_result["reposition_moves"]


def test_compute_min_moves_on_move():
    # arrange
    source_sequence = ["element a", "element b", "element c"]
    target_sequence = list(source_sequence)
    move_source_position = 2
    move_target_position = 1
    moved_element = target_sequence.pop(move_source_position)
    target_sequence.insert(move_target_position, moved_element)
    expected_result = {
        "moves": 1,
        "operations": [
            f"Pop element '{moved_element}' from source index {move_source_position!s} and reinsert at target position {move_target_position!s}."
        ],
        "insertions": [],
        "deletions": [],
        "reposition_moves": [(move_source_position, moved_element, move_target_position)],
    }

    # act
    compute_min_moves_result = compute_min_moves(source_sequence, target_sequence)

    # assert
    assert compute_min_moves_result["moves"] == expected_result["moves"]
    assert compute_min_moves_result["operations"] == expected_result["operations"]
    assert compute_min_moves_result["insertions"] == expected_result["insertions"]
    assert compute_min_moves_result["deletions"] == expected_result["deletions"]
    assert compute_min_moves_result["reposition_moves"] == expected_result["reposition_moves"]
