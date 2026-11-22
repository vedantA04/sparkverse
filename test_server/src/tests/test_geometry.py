import pytest

from control_algs.geometry import Arena, Vec2, degrees2vec, norm180

NORTH, EAST, SOUTH = 0.0, 90.0, 180.0


@pytest.mark.parametrize(
    "rotation, expected",
    [(NORTH, Vec2(0, 1)), (EAST, Vec2(1, 0)), (SOUTH, Vec2(0, -1))],
)
def test_degrees2vec(rotation, expected):
    actual = degrees2vec(rotation)
    assert actual.x == pytest.approx(expected.x, abs=1e-9)
    assert actual.y == pytest.approx(expected.y, abs=1e-9)


@pytest.mark.parametrize(
    "vec, expected",
    [
        (Vec2(0, 1), 0.0),
        (Vec2(1, 0), 90.0),
        (Vec2(0, -1), 180.0),
        (Vec2(-1, 0), 270.0),
        (Vec2(-1, 1), 315.0),
        (Vec2(0, 0), 0.0),
    ],
)
def test_heading(vec, expected):
    assert vec.heading() == pytest.approx(expected)


@pytest.mark.parametrize("degrees", [0.0, 45.0, 135.0, 225.0, 359.0])
def test_heading_inverts_degrees2vec(degrees):
    assert degrees2vec(degrees).heading() == pytest.approx(degrees)


@pytest.mark.parametrize(
    "degrees, expected",
    [
        (0.0, 0.0),
        (90.0, 90.0),
        (-90.0, -90.0),
        (-180, 180),
        (180, 180),
        (270.0, -90.0),
        (-270.0, 90.0),
        (360.0, 0.0),
        (-360.0, 0.0),
        (450.0, 90.0),
        (725.0, 5.0),
        (-725.0, -5.0),
    ],
)
def test_norm180(degrees, expected):
    assert norm180(degrees) == pytest.approx(expected)


def test_arena_from_origin_and_size():
    arena = Arena.from_origin_and_size(origin=(1.0, 2.0), size=(4.0, 2.0))
    assert arena == Arena(min_x=-1.0, max_x=3.0, min_y=1.0, max_y=3.0)
