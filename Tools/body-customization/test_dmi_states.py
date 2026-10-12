import unittest
from dmi_states import states


class DmiOffsetsTests(unittest.TestCase):
    def test_belly_preview_cannot_be_imported_as_first_belly_direction(self):
        description = '''# BEGIN DMI
state = ""
 dirs = 1
 frames = 1
state = "preview_goingtohellonepixelatatime"
 dirs = 4
 frames = 1
state = "belly_pair_1_0_FRONT"
 dirs = 4
 frames = 1
# END DMI'''
        parsed = list(states(description))
        self.assertEqual([(s[0], s[4]) for s in parsed],
                         [('', 0), ('preview_goingtohellonepixelatatime', 1), ('belly_pair_1_0_FRONT', 5)])

    def test_unnamed_animated_states_preserve_later_directions(self):
        description = '''state = ""
 dirs = 4
 frames = 3
state = "butt_pair_1_0_FRONT"
 dirs = 4
 frames = 1
'''
        self.assertEqual(list(states(description))[1][4], 12)


if __name__ == '__main__':
    unittest.main()
