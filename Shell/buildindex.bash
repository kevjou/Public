#!/bin/bash

mkdir -p "hisat2_index"
hisat2-build -p 4 GRCh38.p14.genome.fa mnt/D/hisat2_index/GRCh38