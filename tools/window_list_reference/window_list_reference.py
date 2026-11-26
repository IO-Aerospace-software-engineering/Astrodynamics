"""CSPICE references of the window-list algebra of R16 (#668): WindowList.IntersectWindows, UnionWindows, ExceptWindows
and ComplementWithin against CSPICE's wnintd, wnunid, wndifd and wncomd.

Each case is two lists of closed windows, A and B, their ends whole seconds from an epoch, as the tests build them. Each
list is inserted into a CSPICE window with wninsd (sorted, overlapping and touching windows merged), then the script
prints the windows of wnintd(A, B), wnunid(A, B), wndifd(A, B) and the complement of A within [2, 10], one line per
case, in the form the tests read (WindowListTests): "start end" pairs separated by ';', the six fields separated by '|'.

The complement is wndifd([2, 10], A): it is wncomd(2, 10, A) wherever wncomd stays within [2, 10]. Where an interval of
A ends before 2 and the next one starts after 10, wncomd (SPICELIB 1.1.0) returns [2, that start], past its right
endpoint (A = [0, 1], [11, 14] gives [2, 11]), against its header ("the complement of WINDOW with respect to the
interval from LEFT to RIGHT"); the script prints the complement of the header and counts the cases where wncomd differs.

The cases: by hand, overlapping, touching, zero-length, nested, empty and unsorted lists, and a list of touching windows
(the result of a NotEqual search); then 40 random lists of up to 5 windows on [0, 15] (random.seed(668)), many ends tied.

Run: python3 -I window_list_reference.py (needs spiceypy).
"""
import random

import spiceypy as sp

LEFT, RIGHT = 2.0, 10.0

HAND = [
    ([(0, 4)], [(2, 6)]),  # overlapping
    ([(0, 1)], [(1, 2)]),  # touching
    ([(0, 2)], [(1, 1)]),  # a zero-length window inside another
    ([(1, 1)], [(1, 1)]),  # two equal zero-length windows
    ([(3, 3), (5, 5)], [(5, 8)]),  # zero-length windows, one at the start of the other list's window
    ([(0, 10)], [(2, 3), (5, 7)]),  # nested
    ([], [(1, 2)]),  # empty
    ([(6, 8), (0, 2), (1, 3)], [(4, 5), (2, 2)]),  # unsorted, overlapping within a list
    ([(0, 3), (3, 6), (6, 9)], [(3, 3), (6, 6)]),  # touching windows (NotEqual), and the instants they leave out
    ([(0, 1), (11, 14)], [(12, 13)]),  # where wncomd returns a window past its right endpoint
]


def window(intervals):
    cell = sp.cell_double(2 * max(1, len(intervals)) + 2)
    for start, end in intervals:
        sp.wninsd(float(start), float(end), cell)
    return cell


def intervals(cell):
    return [tuple(sp.wnfetd(cell, i)) for i in range(sp.wncard(cell))]


def text(windows):
    return ';'.join(f'{start:g} {end:g}' for start, end in windows)


def main():
    random.seed(668)
    cases = list(HAND)
    for _ in range(40):
        cases.append(tuple(
            [(x, x + random.choice((0, 0, 1, 2, 3))) for x in (random.randint(0, 12) for _ in range(random.randint(0, 5)))]
            for _ in range(2)))

    differs = 0
    for a, b in cases:
        ca, cb = window(a), window(b)
        complement = intervals(sp.wndifd(window([(LEFT, RIGHT)]), ca))
        if complement != intervals(sp.wncomd(LEFT, RIGHT, ca)):
            differs += 1
        print('|'.join((text(a), text(b), text(intervals(sp.wnintd(ca, cb))), text(intervals(sp.wnunid(ca, cb))),
                        text(intervals(sp.wndifd(ca, cb))), text(complement))))
    print(f'# {sp.tkvrsn("TOOLKIT")}, spiceypy {sp.__version__}: wncomd past its right endpoint in {differs} of {len(cases)} cases')


if __name__ == '__main__':
    main()
