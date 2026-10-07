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
| `operator *(Vector3, Vector3)` | **Dot product** (not a `.Dot()` method) |
| `Cross(Vector3)` | Cross product |
| `Normalize()` | Unit vector |
| `Magnitude()` | Euclidean length |
| `MagnitudeSquared()` | Squared Euclidean length (avoids square root) |
| `Inverse()` | Negated vector (no unary `-` operator) |
| `Rotate(Quaternion)` | Rotate by quaternion |
| `Angle(Vector3)` | Angle between two vectors (rad) |
| `Angle(Vector3, Vector3)` | Angle between vectors with plane normal for sign |
| `To(Vector3)` | Quaternion that rotates this vector to the target vector |
| `LinearInterpolation(Vector3, double)` | Linear interpolation between this and another vector |
| `operator +(Vector3, Vector3)` | Vector addition |
| `operator -(Vector3, Vector3)` | Vector subtraction |
| `operator *(Vector3, double)` | Scalar multiplication |
| `operator /(Vector3, double)` | Scalar division |

!!! warning "Dot Product Convention"
    Use `v1 * v2` for the dot product. There is no `.Dot()` method. `Vector3` has no unary `-` operator; use `.Inverse()` instead.

## Quaternion

`Quaternion` represents a rotation with W (scalar) and VectorPart (X, Y, Z) components.

### Static Constants

| Constant | Description |
|----------|-------------|
| `Quaternion.Zero` | Identity/zero quaternion |

### Constructors

| Constructor | Description |
|-------------|-------------|
| `Quaternion()` | Default (identity) |
| `Quaternion(double w, Vector3 vectorPart)` | Scalar + vector |
| `Quaternion(Vector3 axis, double angle)` | Axis-angle |
| `Quaternion(double w, double x, double y, double z)` | All components |

### Members

| Member | Description |
|--------|-------------|
| `W`, `VectorPart` | Scalar and vector components |
| `Magnitude()` | Quaternion magnitude |
| `Conjugate()` | Inverse rotation |
| `Normalize()` | Unit quaternion |
| `ToEuler()` | Convert to Euler angles (Vector3) |
| `SLERP(Quaternion, double)` | Spherical linear interpolation |
| `Lerp(Quaternion, double)` | Linear interpolation |
| `operator *(Quaternion, Quaternion)` | Quaternion composition |

## Matrix

`Matrix` represents an M x N matrix.

### Constructors

| Constructor | Description |
|-------------|-------------|
| `Matrix(int rows, int columns)` | Zero matrix |
| `Matrix(double[,] data)` | From 2D array |
| `Matrix(int rows, int columns, double[] data)` | From flat array |

### Members

| Member | Description |
|--------|-------------|
| `Rows`, `Columns` | Dimensions |
| `Set(row, col, value)` | Set an element |
| `Get(row, col)` | Get an element |
| `Multiply(Matrix)` | Matrix multiplication |
| `Multiply(double[])` | Matrix-vector multiplication |
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

### Static Methods

| Method | Description |
|--------|-------------|
| `FromRowVectors(row0, row1, row2)` | Create 3x3 from row vectors |
| `FromColumnVectors(col0, col1, col2)` | Create 3x3 from column vectors |
| `FromQuaternion(Quaternion)` | Create 3x3 rotation matrix from quaternion |
| `CreateRotationMatrixX(angle)` | Rotation around X axis |
| `CreateRotationMatrixY(angle)` | Rotation around Y axis |
| `CreateRotationMatrixZ(angle)` | Rotation around Z axis |
| `CreateBlockDiagonal(upperLeft, lowerRight)` | Create block-diagonal matrix |
| `TransformCovariance(covariance, rotation)` | Rotate a 6x6 covariance with `diag(R, R)`; only between inertial frames, use `ToFrame` for a rotating frame |
| `Identity6x6()` | 6x6 identity matrix |

## Lagrange

Lagrange interpolation for polynomial fitting of ephemeris data. Used internally by `PropagationEphemerisCache` for 8-point interpolation of celestial body positions.

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
