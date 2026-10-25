"""Write the lot-1 fixture of the conformance runner's tests (design of #458, section 7.2) from a checkout of the
conformance-tests suite: the categories orbital_elements, time_conversion and frame_transformation.

Written into IO.Astrodynamics.Net/IO.Astrodynamics.ConformanceRunner.Tests/Fixture/Lot1/, which is replaced:
  schemas/v1/case.schema.json, expected.schema.json, runner-report.schema.json
      the schemas of the suite
  tolerances.yaml
      the tolerances of the suite
  cases/<category>/<NNN>/inputs.yaml, expected-result.json
      every case of the three categories; nothing else of the case directories (scripts, reports, logs, checks)
  SOURCE
      the suite commit (git -C <suite> rev-parse HEAD, or the value of --source); without --source, the script stops
      unless <suite> is the root of its own checkout and the files it copies (schemas/v1, tolerances.yaml, data/ and the
      cases of the three categories) are those committed at HEAD, untracked files included
  data.sha256
      the SHA-256 and the name of every file named in the inputs.kernels of a case, one per line
  data/
      those of these files whose SHA-256 differs from IO.Astrodynamics.Tests/Data/SolarSystem, or that are absent there;
      the tests take the others from Data/SolarSystem after checking their SHA-256

The SHA-256 is that of the content as committed: a file without a NUL byte (a text kernel) is hashed with its CRLF
read as LF, so that a Windows checkout, whose Git converts the line endings of text files, gives the hash of Linux.
The tests hash the same way.

Requires PyYAML. A file of data/ left as a Git LFS pointer (a suite cloned without LFS) stops the script.

Usage: python3 -I -B tools/conformance/sync_lot1_fixture.py <suite checkout> [--source <sha>]
"""
import argparse
import hashlib
import pathlib
import shutil
import subprocess
import sys

import yaml

CATEGORIES = ("orbital_elements", "time_conversion", "frame_transformation")
SCHEMAS = ("case.schema.json", "expected.schema.json", "runner-report.schema.json")
CASE_FILES = ("inputs.yaml", "expected-result.json")
COPIED = ("schemas/v1", "tolerances.yaml", "data") + tuple(f"cases/{category}" for category in CATEGORIES)
LFS_POINTER_HEADER = b"version https://git-lfs.github.com/spec/"

REPOSITORY = pathlib.Path(__file__).resolve().parents[2]
NET = REPOSITORY / "IO.Astrodynamics.Net"
FIXTURE = NET / "IO.Astrodynamics.ConformanceRunner.Tests" / "Fixture" / "Lot1"
RUNNER_DATA = NET / "IO.Astrodynamics.Tests" / "Data" / "SolarSystem"


def fail(message):
    sys.exit(f"sync_lot1_fixture: {message}")


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


def suite_commit(suite):
    """HEAD of the suite, once the suite is the root of its checkout and the files copied are those of HEAD."""
    top = git(suite, "rev-parse", "--show-toplevel")
    if pathlib.Path(top).resolve() != suite:
        fail(f"{suite} is not the root of its Git checkout ({top}); give the suite commit with --source")
    commit = git(suite, "rev-parse", "HEAD")
    if git(suite, "status", "--porcelain", "--untracked-files=all", "--", *COPIED):
        fail(f"{suite} has changes not committed at {commit}: commit them or give --source")
    return commit


def case_directories(suite):
    cases = []
    for category in CATEGORIES:
        directory = suite / "cases" / category
        found = sorted(d for d in directory.iterdir() if d.is_dir()) if directory.is_dir() else []
        if not found:
            fail(f"the suite holds no case of {category}")
        for case in found:
            missing = [name for name in CASE_FILES if not (case / name).is_file()]
            if missing:
                fail(f"{case} has no {', '.join(missing)}")
        cases.extend(found)
    return cases


def kernels_of(case):
    inputs = yaml.safe_load((case / "inputs.yaml").read_text(encoding="utf-8")).get("inputs") or {}
    names = inputs.get("kernels") or []
    for name in names:
        if not isinstance(name, str) or name != pathlib.PurePath(name).name or name.startswith("."):
            fail(f"{case / 'inputs.yaml'}: the kernel {name!r} is not a file name of data/")
    return names


def write(suite, source, target):
    cases = case_directories(suite)
    (target / "schemas" / "v1").mkdir(parents=True)
    for schema in SCHEMAS:
        shutil.copyfile(suite / "schemas" / "v1" / schema, target / "schemas" / "v1" / schema)
    shutil.copyfile(suite / "tolerances.yaml", target / "tolerances.yaml")

    kernels = set()
    for case in cases:
        destination = target / case.relative_to(suite)
        destination.mkdir(parents=True)
        for name in CASE_FILES:
            shutil.copyfile(case / name, destination / name)
        kernels.update(kernels_of(case))

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
        runner = RUNNER_DATA / name
        if not runner.is_file() or content_sha256(runner) != hashes[name]:
            (target / "data").mkdir(exist_ok=True)
            shutil.copyfile(path, target / "data" / name)
            shipped.append(name)

    (target / "SOURCE").write_text(f"{source}\n", encoding="ascii", newline="\n")
    (target / "data.sha256").write_text("".join(f"{hashes[name]}  {name}\n" for name in sorted(hashes)),
                                        encoding="ascii", newline="\n")
    return cases, sorted(hashes), shipped


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("suite", type=pathlib.Path, help="checkout of the conformance-tests suite")
    parser.add_argument("--source", help="the suite commit written to SOURCE (default: git rev-parse HEAD)")
    arguments = parser.parse_args()

    suite = arguments.suite.resolve()
    source = arguments.source or suite_commit(suite)

    # Written beside the fixture, then put in its place: a failure leaves the fixture as it was
    staging = FIXTURE.with_name(FIXTURE.name + ".staging")
    if staging.exists():
        shutil.rmtree(staging)
    staging.mkdir(parents=True)
    try:
        cases, kernels, shipped = write(suite, source, staging)
    except BaseException:
        shutil.rmtree(staging)
        raise
    if FIXTURE.exists():
        shutil.rmtree(FIXTURE)
    staging.rename(FIXTURE)

    for category in CATEGORIES:
        print(f"{category}: {sum(1 for case in cases if case.parent.name == category)} case(s)")
    print(f"kernels named: {', '.join(kernels) or 'none'}; shipped in data/: {', '.join(shipped) or 'none'}")
    print(f"source: {source}")
    print(f"written: {FIXTURE}")


if __name__ == "__main__":
    main()
