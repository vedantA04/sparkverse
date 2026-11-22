"""2D vector math and helpers. Headings are in degrees, 0 = north, clockwise
positive."""

import math
import random
from dataclasses import dataclass


@dataclass(frozen=True)
class Vec2:
    x: float
    y: float

    def __add__(self, other: "Vec2") -> "Vec2":
        return Vec2(self.x + other.x, self.y + other.y)

    def __sub__(self, other: "Vec2") -> "Vec2":
        return Vec2(self.x - other.x, self.y - other.y)

    def __neg__(self) -> "Vec2":
        return Vec2(-self.x, -self.y)

    def __mul__(self, scalar: float) -> "Vec2":
        return Vec2(self.x * scalar, self.y * scalar)

    def __truediv__(self, scalar: float) -> "Vec2":
        return Vec2(self.x / scalar, self.y / scalar)

    def length(self) -> float:
        return math.hypot(self.x, self.y)

    def dot(self, other: "Vec2") -> float:
        return self.x * other.x + self.y * other.y

    def normalized(self) -> "Vec2":
        length = self.length()
        return self / length if length > 1e-5 else ZERO

    def clamp_length(self, max_length: float) -> "Vec2":
        length = self.length()
        return self * (max_length / length) if length > max_length else self

    def heading(self) -> float:
        """Heading of this vector in degrees"""
        return math.degrees(math.atan2(self.x, self.y)) % 360.0

    def rot90(self) -> "Vec2":
        """This vector rotated 90 degrees clockwise."""
        return Vec2(self.y, -self.x)


ZERO = Vec2(0.0, 0.0)
EAST = Vec2(1.0, 0.0)
NORTH = Vec2(0.0, 1.0)


@dataclass(frozen=True)
class Arena:
    min_x: float
    max_x: float
    min_y: float
    max_y: float

    @staticmethod
    def from_origin_and_size(
        origin: tuple[float, float], size: tuple[float, float]
    ) -> "Arena":
        (ox, oy), (sx, sy) = origin, size
        return Arena(ox - sx / 2, ox + sx / 2, oy - sy / 2, oy + sy / 2)

    @property
    def center(self) -> Vec2:
        return Vec2((self.min_x + self.max_x) / 2, (self.min_y + self.max_y) / 2)

    def distance_to_edge(self, position: Vec2, direction: Vec2) -> float:
        """Distance from position along a unit direction to the boundary."""
        distances = [math.inf]
        if direction.x > 1e-4:
            distances.append((self.max_x - position.x) / direction.x)
        if direction.x < -1e-4:
            distances.append((self.min_x - position.x) / direction.x)
        if direction.y > 1e-4:
            distances.append((self.max_y - position.y) / direction.y)
        if direction.y < -1e-4:
            distances.append((self.min_y - position.y) / direction.y)
        return min(distances)


def degrees2vec(degrees: float) -> Vec2:
    """Unit vector for a heading in degrees"""
    r = math.radians(degrees)
    return Vec2(math.sin(r), math.cos(r))


def norm180(value: float) -> float:
    """Normalise degrees to (-180, 180]"""
    v360 = value % 360
    return v360 - 360 if v360 > 180 else v360


def random_unit_vector() -> Vec2:
    return degrees2vec(random.uniform(0.0, 360.0))


def clamp(value: float, lo: float, hi: float) -> float:
    return max(lo, min(value, hi))


def clamp01(value: float) -> float:
    return clamp(value, 0.0, 1.0)
