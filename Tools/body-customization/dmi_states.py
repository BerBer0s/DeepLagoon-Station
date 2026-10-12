"""DMI state offsets include unnamed and non-exported states."""
import re


def states(description):
    offset = 0
    for match in re.finditer(r'state = "([^"]*)"(.*?)(?=\nstate = |\n# END|\Z)', description, re.S):
        name, block = match.groups()
        dirs = int(re.search(r'dirs = (\d+)', block)[1])
        frames = int(re.search(r'frames = (\d+)', block)[1])
        yield name, block, dirs, frames, offset
        offset += dirs * frames
