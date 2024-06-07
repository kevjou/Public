#!/bin/bash

# Define the list of HPAP numbers
HPAP_NUMBERS=("006" "053" "070" "079" "097" "105" "106" "109")

# Define the directory path
DIRECTORY="D:/PancDB Donor Files/HPap Analysis/hpapdata/"

# Loop through each HPAP number
for number in "${HPAP_NUMBERS[@]}"
do
    # Define the full directory path for the current HPAP number
    HPAP_DIRECTORY="$DIRECTORY/HPAP-$number/Islet Studies/Islet molecular phenotyping studies/Single-cell RNAseq/Upenn_scRNAseq/fastq"
    
    # Run FastQC on all FastQ files in the current directory
    fastqc "$HPAP_DIRECTORY"/*.fastq.gz --outdir="$HPAP_DIRECTORY"
done
