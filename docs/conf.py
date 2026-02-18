# Configuration file for the Sphinx documentation builder.
#
# This file is kept for backward compatibility.
# For actual builds, use:
# - docs/ru/conf.py for Russian documentation
# - docs/en/conf.py for English documentation
#
# This file will import the Russian configuration by default.

import os
import sys

# Import Russian config by default
sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'ru'))
from conf import *
