#!/bin/bash
#
# author: abu
# date:   May 12 2018
# description: bash script to install pdftk on Ubuntu 18.04 for amd64 machines
##############################################################################
#
# change to /tmp directory
cd /tmp
# download packages
wget http://mirrors.edge.kernel.org/ubuntu/pool/main/g/gcc-5/libgcj16_5.4.0-6ubuntu1~16.04.10_amd64.deb \
    http://mirrors.edge.kernel.org/ubuntu/pool/main/g/gcc-defaults/libgcj-common_4.9.3-9ubuntu1_all.deb \
    http://mirrors.edge.kernel.org/ubuntu/pool/universe/p/pdftk/pdftk_2.02-4_amd64.deb \
    http://mirrors.edge.kernel.org/ubuntu/pool/universe/p/pdftk/pdftk-dbg_2.02-4_amd64.deb

echo -e "Packages for pdftk downloaded\n\n"
# install packages 
echo -e "\n\n Installing pdftk: \n\n"
sudo apt-get install ./libgcj16_5.4.0-6ubuntu1~16.04.10_amd64.deb \
    ./libgcj-common_4.9.3-9ubuntu1_all.deb \
    ./pdftk_2.02-4_amd64.deb \
    ./pdftk-dbg_2.02-4_amd64.deb
echo -e "\n\n pdftk installed\n"
echo -e "   try it in shell with: > pdftk \n"
# delete deb files in /tmp directory
rm ./libgcj16_5.4.0-6ubuntu1~16.04.10_amd64.deb
rm ./libgcj-common_4.9.3-9ubuntu1_all.deb
rm ./pdftk_2.02-4_amd64.deb
rm ./pdftk-dbg_2.02-4_amd64.deb
    http://mirrors.edge.kernel.org/ubuntu/pool/universe/p/pdftk/pdftk-dbg_2.02-4_amd64.deb

echo -e "Packages for pdftk downloaded\n\n"
# install packages
echo -e "\n\n Installing pdftk: \n\n"
sudo apt-get install ./libgcj16_5.4.0-6ubuntu1~16.04.10_amd64.deb \
    ./libgcj-common_4.9.3-9ubuntu1_all.deb \
    ./pdftk_2.02-4_amd64.deb \
    ./pdftk-dbg_2.02-4_amd64.deb
echo -e "\n\n pdftk installed\n"
echo -e "   try it in shell with: > pdftk \n"
# delete deb files in /tmp directory
rm ./libgcj16_5.4.0-6ubuntu1~16.04.10_amd64.deb
rm ./libgcj-common_4.9.3-9ubuntu1_all.deb
rm ./pdftk_2.02-4_amd64.deb
rm ./pdftk-dbg_2.02-4_amd64.deb

