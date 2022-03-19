﻿using System;
using System.Collections.Generic;

namespace SlipeServer.Packets.Constants
{
    public static class VehicleConstants
    {
        public static HashSet<int> VehiclesWithTurrets { get; } = new()
        {
            407,
            432,
            601,
        };

        public static HashSet<int> VehiclesWithAdjustableProperties { get; } = new()
        {
            406, 443, 486, 520, 524, 525, 530, 531, 592,

        };

        public static HashSet<int> VehiclesWithDoors { get; } = new()
        {
            400, 401, 402, 403, 404, 405, 406, 407, 408, 409, 410, 411, 412, 413, 414, 415,
	        416, 417, 418, 419, 420, 421, 422, 423, 425, 426, 427, 428, 429, 430, 431, 432, 433,
	        434, 435, 436, 437, 438, 439, 440, 442, 443, 444, 445, 446, 447, 448, 449, 450, 451,
	        452, 453, 454, 455, 456, 458, 459, 460, 461, 462, 463, 464, 466, 467, 468, 469,
	        470, 471, 472, 473, 474, 475, 476, 477, 478, 479, 480, 481, 482, 483, 484, 487,
	        488, 489, 490, 491, 492, 493, 494, 495, 496, 497, 498, 499, 500, 502, 503, 504, 505,
	        506, 507, 508, 509, 510, 511, 512, 513, 514, 515, 516, 517, 518, 519, 520, 521, 522, 523,
	        524, 525, 526, 527, 528, 529, 532, 533, 534, 535, 536, 537, 538, 539, 540, 541,
	        542, 543, 544, 545, 546, 547, 548, 549, 550, 551, 552, 553, 554, 555, 556, 557, 558, 559,
	        560, 561, 562, 563, 565, 566, 567, 569, 570, 573, 574, 575, 576, 577,
	        578, 579, 580, 581, 582, 583, 584, 585, 586, 587, 588, 589, 590, 591, 592, 593, 595,
	        596, 597, 598, 599, 600, 601, 602, 603, 604, 605, 606, 607, 608, 609, 610, 611
        };
    }
}
