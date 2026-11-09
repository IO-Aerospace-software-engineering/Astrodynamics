"""Write a fixture of the conformance runner's tests from a checkout of the conformance-tests suite: lot 1 (design of
#458, section 7.2), the categories orbital_elements, time_conversion and frame_transformation; lot 2 (design of #500,
section 6.4), the propagator cases 006 to 018; lot 3 (design of #587, section 8.2), the categories maneuver and
lambert; lot 4 (design of lot 4 of #457, section 8.2), the categories ground_station, eclipse, orbital_events and
attitude; lot 5 (design of lot 5 of #457, section 8.2), the categories sgp4 and tle_conversion and the TEME cases 032 to
036 of frame_transformation (lot 1 keeps its cases 001 to 031); lot 6 (design of lot 6 of #457, section 8.2), the
categories conjunction, collision_probability, cdm and stm.

Written into IO.Astrodynamics.Net/IO.Astrodynamics.ConformanceRunner.Tests/Fixture/Lot<n>/, which is replaced:
  schemas/v1/case.schema.json, expected.schema.json, runner-report.schema.json
      the schemas of the suite
  tolerances.yaml
      the tolerances of the suite
  cases/<category>/<NNN>/inputs.yaml, expected-result.json
      every case of the lot, and the CDM a cdm case reads next to its inputs.yaml (inputs.cdm_file); nothing else of the
      case directories (scripts, reports, logs, checks, the informative KVN source of a CDM)
  SOURCE
      the suite commit (git -C <suite> rev-parse HEAD, or the value of --source); without --source, the script stops
      unless <suite> is the root of its own checkout and the files it copies (schemas/v1, tolerances.yaml, data/ and the
      cases of the lot's categories) are those committed at HEAD, untracked files included
  data.sha256
      the SHA-256 and the name of every file of data/ a case reads: those named in its inputs.kernels and, for lots 2
      to 4 and 6, its geopotential_model (the field file, which the runner reads from data/); for lot 4, the SPK of a
      bsp_source orbit (orbit.filename, data/<name>) and the field file EGM2008_to70_TideFree, whose header carries the
      GM of a two-body trajectory (the runner's TwoBodyTrajectory); one per line
  data/
      those of these files whose SHA-256 differs from IO.Astrodynamics.Tests/Data/SolarSystem (and, for lot 4,
      IO.Astrodynamics.Tests/Data/LRO) and from the data/ of the fixtures of the earlier lots, or that are absent there;
      the tests take the others from those directories after checking their SHA-256

The SHA-256 is that of the content as committed: a file without a NUL byte (a text kernel) is hashed with its CRLF
read as LF, so that a Windows checkout, whose Git converts the line endings of text files, gives the hash of Linux.
The tests hash the same way.

Requires PyYAML. A file of data/ left as a Git LFS pointer (a suite cloned without LFS) stops the script.

Usage: python3 -I -B tools/conformance/sync_fixture.py --lot <1|2|3|4|5|6> <suite checkout> [--source <sha>]
"""
import argparse
import hashlib
import pathlib
import shutil
import subprocess
import sys

import yaml

SCHEMAS = ("case.schema.json", "expected.schema.json", "runner-report.schema.json")
CASE_FILES = ("inputs.yaml", "expected-result.json")
LFS_POINTER_HEADER = b"version https://git-lfs.github.com/spec/"
NO_FIELD = "none"

REPOSITORY = pathlib.Path(__file__).resolve().parents[2]
NET = REPOSITORY / "IO.Astrodynamics.Net"
FIXTURES = NET / "IO.Astrodynamics.ConformanceRunner.Tests" / "Fixture"
RUNNER_DATA = NET / "IO.Astrodynamics.Tests" / "Data" / "SolarSystem"
TESTS_LRO = NET / "IO.Astrodynamics.Tests" / "Data" / "LRO"
DATA_PREFIX = "data/"


class Lot:
    """The cases of a lot: every case of its categories, or those numbered first to last; ranges: the first and last
    numbers of a category whose cases the lot shares with another lot, which override first and last for it; fields:
    whether the runner reads the case's geopotential_model from data/; always: the files of data/ the runner reads for
    every case of the lot; spks: whether it reads the SPK of a bsp_source orbit from data/; reference: further
    directories of the repository whose files the tests take when the fixture does not ship them."""

    def __init__(self, number, categories, first=None, last=None, fields=False, always=(), spks=False, reference=(),
                 ranges=None):
        self.number = number
        self.categories = categories
        self.first = first
        self.last = last
        self.ranges = ranges or {}
        self.fields = fields
        self.always = always
        self.spks = spks
        self.reference = reference

    @property
    def fixture(self):
        return FIXTURES / f"Lot{self.number}"

    @property
    def copied(self):
        return ("schemas/v1", "tolerances.yaml", "data") + tuple(f"cases/{category}" for category in self.categories)

    def selects(self, case):
        number = int(case.name)
        first, last = self.ranges.get(case.parent.name, (self.first, self.last))
        return (first is None or number >= first) and (last is None or number <= last)

    def reference_data(self):
        """The directories the tests take a file from when the fixture does not ship it: the runner's kernels, then
        the data/ of the fixtures of the earlier lots."""
        return [RUNNER_DATA, *self.reference] + [LOTS[n].fixture / "data" for n in sorted(LOTS) if n < self.number]


LOTS = {
    1: Lot(1, ("orbital_elements", "time_conversion", "frame_transformation"), ranges={"frame_transformation": (1, 31)}),
    2: Lot(2, ("propagator",), first=6, last=18, fields=True),
    3: Lot(3, ("maneuver", "lambert"), fields=True),
    4: Lot(4, ("ground_station", "eclipse", "orbital_events", "attitude"), fields=True, always=("EGM2008_to70_TideFree",),
           spks=True, reference=(TESTS_LRO,)),
    5: Lot(5, ("sgp4", "tle_conversion", "frame_transformation"), ranges={"frame_transformation": (32, 36)}),
    6: Lot(6, ("conjunction", "collision_probability", "cdm", "stm"), fields=True),
}


def fail(message):
    sys.exit(f"sync_fixture: {message}")


def content_sha256(path):
    data = path.read_bytes()
    if b"\0" not in data:
        data = data.replace(b"\r\n", b"\n")
    return hashlib.sha256(data).hexdigest()


def git(suite, *arguments):
    try:
        return subprocess.run(["git", "-C", str(suite), *arguments], check=True, capture_output=True,
                              text=True).stdout.strip()
    except (OSError, subprocess.CalledProcessError) as error:
        fail(f"cannot read the commit of {suite} ({error}); give it with --source")


def suite_commit(suite, lot):
    """HEAD of the suite, once the suite is the root of its checkout and the files copied are those of HEAD."""
    top = git(suite, "rev-parse", "--show-toplevel")
    if pathlib.Path(top).resolve() != suite:
        fail(f"{suite} is not the root of its Git checkout ({top}); give the suite commit with --source")
    commit = git(suite, "rev-parse", "HEAD")
    if git(suite, "status", "--porcelain", "--untracked-files=all", "--", *lot.copied):
        fail(f"{suite} has changes not committed at {commit}: commit them or give --source")
    return commit


def case_files(case):
    """The files of a case the runner reads: its inputs and golden, and the CDM its inputs name (inputs.cdm_file)."""
    inputs = yaml.safe_load((case / "inputs.yaml").read_text(encoding="utf-8")).get("inputs") or {}
    names = list(CASE_FILES)
    cdm = inputs.get("cdm_file")
    if cdm is not None:
        if not isinstance(cdm, str) or cdm != pathlib.PurePath(cdm).name or cdm.startswith("."):
            fail(f"{case / 'inputs.yaml'}: the CDM {cdm!r} is not a file name next to it")
        names.append(cdm)
    return names


def case_directories(suite, lot):
    cases = []
    for category in lot.categories:
        directory = suite / "cases" / category
        found = sorted(d for d in directory.iterdir() if d.is_dir() and lot.selects(d)) if directory.is_dir() else []
        if not found:
            fail(f"the suite holds no case of lot {lot.number} in {category}")
        for case in found:
            missing = [name for name in CASE_FILES if not (case / name).is_file()]
            if not missing:
                missing = [name for name in case_files(case) if not (case / name).is_file()]
            if missing:
                fail(f"{case} has no {', '.join(missing)}")
        cases.extend(found)
    return cases


def data_files_of(case, lot):
    """The files of data/ the case reads: its kernels, the files the lot reads for every case and, when the lot's
    runner reads them from data/, its field and the SPK of its orbit."""
    inputs = yaml.safe_load((case / "inputs.yaml").read_text(encoding="utf-8")).get("inputs") or {}
    names = list(inputs.get("kernels") or []) + list(lot.always)
    field = inputs.get("geopotential_model")
    if lot.fields and field is not None and field != NO_FIELD:
        names.append(field)
    orbit = inputs.get("orbit") or {}
    if lot.spks and orbit.get("type") == "bsp_source":
        filename = str(orbit.get("filename"))
        if not filename.startswith(DATA_PREFIX):
            fail(f"{case / 'inputs.yaml'}: the SPK {filename!r} is not a file of data/")
        names.append(filename[len(DATA_PREFIX):])
    for name in dict.fromkeys(names):
        if not isinstance(name, str) or name != pathlib.PurePath(name).name or name.startswith("."):
            fail(f"{case / 'inputs.yaml'}: the file {name!r} is not a file name of data/")
    return list(dict.fromkeys(names))


def write(suite, source, target, lot):
    cases = case_directories(suite, lot)
    (target / "schemas" / "v1").mkdir(parents=True)
    for schema in SCHEMAS:
        shutil.copyfile(suite / "schemas" / "v1" / schema, target / "schemas" / "v1" / schema)
    shutil.copyfile(suite / "tolerances.yaml", target / "tolerances.yaml")

    kernels = set()
    for case in cases:
        destination = target / case.relative_to(suite)
        destination.mkdir(parents=True)
        for name in case_files(case):
            shutil.copyfile(case / name, destination / name)
        kernels.update(data_files_of(case, lot))

    hashes = {}
    shipped = []
    for name in sorted(kernels):
        path = suite / "data" / name
        if not path.is_file():
            fail(f"{path}, named by a case, does not exist")
        with path.open("rb") as stream:
            if stream.read(len(LFS_POINTER_HEADER)) == LFS_POINTER_HEADER:
                fail(f"{path} is a Git LFS pointer: run 'git lfs install && git lfs pull' in the suite")
        hashes[name] = content_sha256(path)
        if not any((directory / name).is_file() and content_sha256(directory / name) == hashes[name]
                   for directory in lot.reference_data()):
            (target / "data").mkdir(exist_ok=True)
            shutil.copyfile(path, target / "data" / name)
            shipped.append(name)

    (target / "SOURCE").write_text(f"{source}\n", encoding="ascii", newline="\n")
    (target / "data.sha256").write_text("".join(f"{hashes[name]}  {name}\n" for name in sorted(hashes)),
                                        encoding="ascii", newline="\n")
    return cases, sorted(hashes), shipped


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--lot", type=int, choices=sorted(LOTS), required=True, help="the lot of the fixture")
    parser.add_argument("suite", type=pathlib.Path, help="checkout of the conformance-tests suite")
    parser.add_argument("--source", help="the suite commit written to SOURCE (default: git rev-parse HEAD)")
    arguments = parser.parse_args()

    lot = LOTS[arguments.lot]
    suite = arguments.suite.resolve()
    source = arguments.source or suite_commit(suite, lot)

    # Written beside the fixture, then put in its place: a failure leaves the fixture as it was
    fixture = lot.fixture
    staging = fixture.with_name(fixture.name + ".staging")
    if staging.exists():
        shutil.rmtree(staging)
    staging.mkdir(parents=True)
    try:
        cases, kernels, shipped = write(suite, source, staging, lot)
    except BaseException:
        shutil.rmtree(staging)
        raise
    if fixture.exists():
        shutil.rmtree(fixture)
    staging.rename(fixture)

    for category in lot.categories:
        print(f"{category}: {sum(1 for case in cases if case.parent.name == category)} case(s)")
    print(f"data files named: {', '.join(kernels) or 'none'}; shipped in data/: {', '.join(shipped) or 'none'}")
    print(f"source: {source}")
    print(f"written: {fixture}")


if __name__ == "__main__":
    main()
