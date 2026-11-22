# Math Utilities

Core geometry, linear algebra, interpolation, and physics utilities used throughout the framework.

## Vector3

`Vector3` represents a 3D vector with immutable X, Y, Z components.

### Static Constants

| Constant | Value | Description |
|----------|-------|-------------|
| `Vector3.VectorX` | (1, 0, 0) | Unit X vector |
| `Vector3.VectorY` | (0, 1, 0) | Unit Y vector |
| `Vector3.VectorZ` | (0, 0, 1) | Unit Z vector |
| `Vector3.Zero` | (0, 0, 0) | Zero vector |

### Members

| Member | Description |
|--------|-------------|
| `X`, `Y`, `Z` | Components |
| `Dot(Vector3)` | Dot product, x·x' + y·y' + z·z' (#668, R26: the operator `*` between two vectors, removed in 11.0.0) |
| `Cross(Vector3)` | Cross product |
| `Normalize()` | Unit vector of any finite non-zero vector; `InvalidOperationException` for the zero vector (#680) or a component that is not finite (third round of #668). A vector whose squared components would overflow or lose their digits (a component above about 1.34e154, or all below about 1e-150) is first scaled exactly by the power of two of its largest component; every other vector is divided by its unscaled magnitude, bit for bit |
| `Magnitude()` | Euclidean length, scaled as `Normalize()` is when the squares would overflow or underflow: +Infinity only above `double.MaxValue`, 0 only for the zero vector (third round of #668: +Infinity for (1e200, 0, 0) and 0 for (1e-170, 0, 0) before 11.0.0); NaN for a NaN component, +Infinity for an infinite one |
| `MagnitudeSquared()` | Squared Euclidean length (avoids square root), unscaled: +Infinity above about 1.34e154, 0 below about 1.5e-162 |
| `Inverse()` | Negated vector, the same as unary `-` |
| `Rotate(Quaternion)` | Rotate by quaternion |
| `Angle(Vector3)` | Angle between two vectors (rad), in [0, π]; 0 when either is zero, NaN for a non-finite component; vectors outside about 1e-150 to 1e150 compared through their scaled unit vectors (third round of #668: 0 or NaN before 11.0.0) |
| `Angle(Vector3, Vector3)`, `Angle(Vector3, Plane)` | Signed angle about a plane normal of any finite non-zero length (a tiny or huge one included, third round of #668), atan2((a × b) · n̂, a · b), in (−π, π]; vectors whose sums of squares are not both in [1e-300, 1e300] scaled first by the powers of two of their largest components, exactly (third round of #668: π/4 instead of atan 2 from (1e200, 0, 0) to (1e200, 2e200, 0), NaN or 0 before 11.0.0), every other pair bit for bit; 0 when a vector is zero, NaN for a vector with a non-finite component, as `Angle(Vector3)`; `ArgumentException` (`plane`) for a zero or non-finite normal (#680's known limitation) |
| `To(Vector3)` | Unit quaternion of the shortest rotation from the direction of this vector to the direction of the target: the identity for the same direction, a half turn (w = 0) about a perpendicular axis for opposite directions (also when they are collinear in doubles and `Angle` is not exactly π); lengths of any finite non-zero size; `InvalidOperationException` for a zero or non-finite instance, `ArgumentException` (`vector`) for a zero or non-finite target (review of #680's known limitation; the identity, NaN or the zero quaternion before 11.0.0). Opposite vectors whose cross product is a rounding residue can still miss the target |
| `LinearInterpolation(Vector3, double)` | Linear interpolation between this and another vector |
| `operator +(Vector3, Vector3)` | Vector addition |
| `operator -(Vector3, Vector3)` | Vector subtraction |
| `operator -(Vector3)` | Negated vector, (−x, −y, −z) |
| `operator *(Vector3, double)`, `operator *(double, Vector3)` | Scalar multiplication, the same vector in either order |
| `operator *(Matrix, Vector3)` | Product of a 3 × 3 matrix and a column vector, the arithmetic of `Matrix.Multiply(double[])`; `ArgumentException` (`m`) for a matrix that is not 3 × 3 |
| `operator /(Vector3, double)` | Scalar division |

!!! warning "Dot product"
    The dot product is `v1.Dot(v2)`. The operator `*` between two vectors, which was the dot product before 11.0.0
    (component-wise in `System.Numerics`), is removed (#668, R26): `v1 * v2` no longer compiles.

```csharp
var r = new Vector3(7000e3, 0.0, 0.0);
var v = new Vector3(0.0, 7.5e3, 0.0);
double radialVelocity = r.Dot(v) / r.Magnitude();   // 0
Vector3 opposite = -r;                               // (−7000e3, 0, 0)
Vector3 doubled = 2.0 * r;                           // (14000e3, 0, 0)
Vector3 turned = Matrix.CreateRotationMatrixZ(Constants.HalfPi) * r;   // (0, 7000e3, 0) to rounding
```

## Plane

`Plane` is a normal and a distance from the origin (`Plane.X`, `Plane.Y`, `Plane.Z`: the planes of unit normals along
the axes, through the origin). The constructors take the normal as it is; `default(Plane)` and `new Plane()` have the zero
normal, which orients no plane. It is a `readonly record struct` (11.0.0: not read only before; its properties already had no setter).

| Member | Description |
|--------|-------------|
| `Normal`, `Distance` | Normal and distance from the origin |
| `GetAngle(Plane)` | Angle between the two normals, acos((n1 · n2) / (\|n1\| \|n2\|)), in [0, π], the quotient clamped to [-1, 1] (bit for bit the former quotient when both sums of squares lie in [1e-300, 1e300], the scaled unit vectors of `Normalize` otherwise) (parallel normals 0, antiparallel π, near which acos is sensitive: about 3e-8 rad); `InvalidOperationException` for a zero or non-finite normal of this plane, `ArgumentException` (`plane`) for one of the argument (third round of #668: NaN before 11.0.0, also for about a quarter of parallel normals and for normals outside about 1e-154 to 1e154) |
| `GetAngle(Vector3)` | Angle of the vector to the plane, asin((n · v) / (\|n\| \|v\|)), in [-π/2, π/2], positive on the side of the normal, clamped likewise; `InvalidOperationException` for a zero or non-finite normal of this plane, `ArgumentException` (`vector`) for a zero or non-finite vector |

## Quaternion

`Quaternion` represents a rotation with W (scalar) and VectorPart (X, Y, Z) components.

### Static Constants

| Constant | Description |
|----------|-------------|
| `Quaternion.Identity` | The identity rotation (1, 0, 0, 0), equal to `new Quaternion()`; `static readonly` |

`Quaternion.Identity` replaces `Quaternion.Zero` of the versions before 11.0.0, which was the identity under another
name and an assignable field ([#571](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/571)).
There is no zero-quaternion constant: (0, 0, 0, 0) is not a rotation; `new Quaternion(0, 0, 0, 0)` builds it.

### Constructors

| Constructor | Description |
|-------------|-------------|
| `Quaternion()` | Default (identity) |
| `Quaternion(double w, Vector3 vectorPart)` | Scalar + vector |
| `Quaternion(Vector3 axis, double angle)` | Axis-angle: the rotation by `angle` about the direction of `axis`, which is divided by its magnitude (#688; a unit axis gives the same quaternion bit for bit); `ArgumentException` for a zero axis or a component that is not finite |
| `Quaternion(double w, double x, double y, double z)` | All components |

### Members

| Member | Description |
|--------|-------------|
| `W`, `VectorPart` | Scalar and vector components |
| `Magnitude()` | Quaternion magnitude, scaled as `Normalize()` is when the squares would overflow or underflow (third round of #668) |
| `Conjugate()` | Inverse rotation |
| `Normalize()` | Unit quaternion of any finite non-zero quaternion; `InvalidOperationException` for the zero quaternion (#680) or a component that is not finite (third round of #668); scaled exactly by the power of two of its largest component outside about 1e-154 to 1e154, bit for bit the quaternion over its unscaled magnitude otherwise |
| `ToEuler()` | Convert to Euler angles (Vector3) |
| `SLERP(Quaternion, double)` | Spherical linear interpolation of the two rotations, on the shorter path: both quaternions normalized first (any finite non-zero magnitude); `InvalidOperationException` for a zero or non-finite instance, `ArgumentException` (`q`) for a zero or non-finite argument |
| `Lerp(Quaternion, double)` | Linear interpolation q + t (q2 − q), normalized, on the chord as given, for quaternions of any finite non-zero magnitude (a chord that overflows, or of quaternions whose largest component is below 1e-150, computed on both scaled by one power of two, third round of #668; every other chord bit for bit); `InvalidOperationException` for a zero or non-finite instance, `ArgumentException` (`q2`) for a zero or non-finite `q2` or a chord through zero (q to −q at t = 0.5) |
| `operator *(Quaternion, Quaternion)` | Quaternion composition |

## Matrix

`Matrix` represents an M x N matrix, immutable: no member changes a matrix once built (#668, R26). `default(Matrix)` is the
0 × 0 matrix for every member.

### Constructors

| Constructor | Description |
|-------------|-------------|
| `Matrix(int rows, int columns)` | Zero matrix; `ArgumentOutOfRangeException` for a negative dimension |
| `Matrix(double[,] data)` | From 2D array, copied: a later write to the array does not change the matrix (#745) |
| `Matrix(int rows, int columns, double[] data)` | From flat array, row-major, copied |

### Members

| Member | Description |
|--------|-------------|
| `Rows`, `Columns` | Dimensions |
| `this[row, col]`, `Get(row, col)` | Get an element; `ArgumentOutOfRangeException` (`row` or `column`) for an index outside the matrix (an `IndexOutOfRangeException` before 11.0.0) |
| `With(row, col, value)` | A copy with one element changed, the matrix unchanged |
| `Multiply(Matrix)` | Matrix multiplication |
| `Multiply(double[])` | The column product M v, `Rows` components, each the sum of m[i, j] · v[j] in column order, the same as `M * v`; `ArgumentException` (`vector`) for a vector of another length than `Columns` (#774: before 11.0.0 the result had the vector's length) |
| `Transpose()` | Transposed matrix |
| `Inverse()` | Matrix inverse |
| `Add(Matrix)` | Element-wise addition |
| `SubMatrix(startRow, startCol, rows, cols)` | Extract sub-matrix |
| `Cholesky(tolerance)` | Cholesky decomposition |
| `IsSymmetric(tolerance)` | Symmetry check |
| `ToQuaternion()` | Convert 3x3 rotation matrix to quaternion |
| `ToArray()` | Convert to 2D array |
| `operator +(Matrix, Matrix)` | Matrix addition |
| `operator *(Matrix, Matrix)` | Matrix multiplication |
| `operator *(Matrix, double[])` | `M * v`, the column product M v, `Rows` components, the same as `Multiply(double[])`; `ArgumentException` (`rhs`) for a vector of another length than `Columns` |
| `operator *(double[], Matrix)` | `v * M`, the row product vᵀ M, `Columns` components, each the sum of v[i] · m[i, j] in row order (the components of Mᵀ v); `ArgumentException` (`lhs`) for a vector of another length than `Rows` (#774: M v before 11.0.0, which equals vᵀ M only for a symmetric matrix) |
| `Equals(Matrix)`, `==`, `!=` | Same dimensions and every element equal by `double.Equals`: NaN equals NaN only, +0 equals −0 (#680) |
| `ToString()` | The rows, `[[1, 0], [0, 1]]`, each element in invariant culture and round trip; `[]` without rows (the record's `Matrix { Rows = 2, Columns = 2 }` before 11.0.0) |

A `Matrix` cannot be changed once built: a covariance given to a `StateVector` or read from it, or any other matrix a
type holds or returns, keeps its terms whatever the caller does afterwards. Before 11.0.0 a public `Set` wrote the array
every copy of the struct shares (#722 then made the holders copy in and out; #745 made `new Matrix(double[,])` copy its
array). A matrix is built from an array, a function of the indexes, a diagonal or as the identity, and changed by
copies:

```csharp
var cov = Matrix.Diagonal(100.0, 100.0, 100.0, 1.0, 1.0, 1.0);           // m², m²/s²
var correlated = cov.With(1, 3, -5.0).With(3, 1, -5.0);                  // cov unchanged
var block = Matrix.Create(3, 3, (i, j) => correlated[i + 3, j + 3]);
double sigmaX = System.Math.Sqrt(cov[0, 0]);                            // 10 m
```

### Static Methods

| Method | Description |
|--------|-------------|
| `FromRowVectors(row0, row1, row2)` | Create 3x3 from row vectors |
| `FromColumnVectors(col0, col1, col2)` | Create 3x3 from column vectors |
| `FromQuaternion(Quaternion)` | Create 3x3 rotation matrix from the unit quaternion of a quaternion of any finite non-zero magnitude; `ArgumentException` (`q`) for a zero or non-finite quaternion (#680's known limitation; the identity before 11.0.0) |
| `CreateRotationMatrixX(angle)` | Rotation around X axis |
| `CreateRotationMatrixY(angle)` | Rotation around Y axis |
| `CreateRotationMatrixZ(angle)` | Rotation around Z axis |
| `CreateBlockDiagonal(upperLeft, lowerRight)` | Create block-diagonal matrix |
| `TransformCovariance(covariance, rotation)` | Rotate a 6x6 covariance with `diag(R, R)`; only between inertial frames, use `ToFrame` for a rotating frame; `ArgumentException` (`rotation`) for a zero or non-finite quaternion |
| `Create(rows, columns, term)` | The matrix of the terms `term(i, j)`, called once per term, row by row; `ArgumentOutOfRangeException` for a negative dimension |
| `Diagonal(params double[] diagonal)` | Square matrix of the diagonal given, zeros elsewhere |
| `Identity(int dimension)` | Identity matrix of any dimension; `ArgumentOutOfRangeException` for a negative one |
| `Identity6x6()` | 6x6 identity matrix, `Identity(6)` |

## Lagrange

Lagrange interpolation for polynomial fitting of ephemeris data. Used internally by the ephemeris cache of a propagation (internal since 11.0.0) for 8-point interpolation of celestial body positions.

## Tsiolkovski

Rocket equation utilities for propulsion calculations. All methods are static.

| Method | Description |
|--------|-------------|
| `DeltaV(double isp, double initialMass, double finalMass)` | Compute delta-V from mass ratio |
| `DeltaT(double isp, double initialMass, double fuelFlow, double deltaV)` | Compute burn duration |
| `DeltaM(double isp, double initialMass, double deltaV)` | Compute fuel mass consumed |

```csharp
double dv = Tsiolkovski.DeltaV(isp: 300, initialMass: 1000, finalMass: 800);
TimeSpan burnTime = Tsiolkovski.DeltaT(isp: 300, initialMass: 1000, fuelFlow: 0.5, deltaV: dv);
double fuelUsed = Tsiolkovski.DeltaM(isp: 300, initialMass: 1000, deltaV: dv);
```

## Legendre Functions

Associated Legendre functions with geodesy normalization (no Condon-Shortley phase). Used internally by `GeopotentialGravitationalField` for spherical harmonic gravity.

## See Also

- [StateVector](state-vector.md)
- [Frames & Orientation](frames.md)
- [Force Models](force-models.md)
