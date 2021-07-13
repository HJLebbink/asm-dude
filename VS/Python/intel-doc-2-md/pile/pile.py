#!/usr/bin/env python

import os, sys
import fnmatch
import argparse
import requests, json

from pprint import pprint as pp

PILE_MAIN_DIR   = os.path.abspath(os.path.dirname(__file__))
ROOT_DIR        = os.path.abspath(os.getcwd())
PILE_CONF_DIR   = "%s/.pile" % ROOT_DIR
PKG_DIR         = "packages"

class Pile(object):
    def __init__(self): 
        self.config_file_path = "%s/config.json" % PILE_CONF_DIR
    
    def __load_config(self):
        try:
            self.config = json.load(open(self.config_file_path))
        except IOError, e:
            print
            print "Please make sure to run pile -n (pile --new) before installing packages"
            
            sys.exit(-1)
            
    def __write_config(self):
        json.dump(self.config, open(self.config_file_path, "w+"))
    
    def new(self):
        """ Setup environment for Pile"""
        if not os.path.isdir(PILE_CONF_DIR):
            os.makedirs(PILE_CONF_DIR)

        try:
            self.config = json.load(open(self.config_file_path))
        except IOError, e:
            conf = open(self.config_file_path, "w+")
            self.config = {}
            self.js_dir()
        
            
    def js_dir(self, directory="js"):
        self.config['dir'] = directory
        self.__write_config()
        
    def update(self, val=None):
        print
        print "Updating pile..."
        os.chdir(PILE_MAIN_DIR)
        
        print "Pulling new packages..."
        os.popen("git pull")
        print "Update complete"
        
    def search(self, name):
        self.__load_config()
        
        recipe_path     = "%s/%s" % (PILE_MAIN_DIR, PKG_DIR)
        
        print
        print "Searching for %s..." % name

        for file in os.listdir(recipe_path):
            if fnmatch.fnmatch(file, "*%s*.json" % name):
                package = json.load(open(recipe_path + "/" + file))
                print "%s (%s): %s" % (package['name'], package['version'], package['description'])
                
        print
        
    def install(self, lib):
        self.__load_config()
        
        lib_path        = "%s/%s/%s.json" % (PILE_MAIN_DIR, PKG_DIR, lib)
        cache_path      = "%s/.pile/%s/%s.json" % (ROOT_DIR, PKG_DIR, lib)
        install_path    = "%s/%s" % (ROOT_DIR, self.config.get('dir'))
        
        if not os.path.isdir(install_path):
            os.makedirs(install_path)
        
        if not os.path.isdir(PILE_CONF_DIR):
            os.makedirs(PILE_CONF_DIR)
        
        package = json.load(open(lib_path))
        
        result = requests.get(package.get("url"))
        name = package.get("url").split("/")
        name = name[len(name) - 1]
        
        script = open("%s/%s" % (install_path, name), "w+")
        script.write(result.content)
        
        if not self.config.get("installed_packages"):
            self.config["installed_packages"] = []
            
        self.config["installed_packages"].append(package.get("name"))
            
        json.dump(package, open(cache_path, "w+"))
        self.__write_config()
        
    def build(self):
        pass
        
    def compress(self, val=None):
        pass
        
    def clear(self):
        pass
        
        
if __name__ == "__main__":
    pile = Pile()
    
    parser = argparse.ArgumentParser(
        prog="Pile",
        description = """
        \nClient side javascript package manager.
        \nPile builds a collection of client side javascript files and
        \ncan either create and manage a js folder or a single, combined, file generated.
        \n
        """
    )

    parser.add_argument('-n', 'new'     , action="store_true")
    parser.add_argument('-u', 'update'  , action="store_true")
    parser.add_argument('-s', 'search'  , type=pile.search, help="Search for a library")
    parser.add_argument('-i', 'install' , type=pile.install, help="Install a Javascript library")
    parser.add_argument('-b', 'build'   , type=pile.build)
    parser.add_argument('-c', 'compress', type=pile.compress)
    parser.add_argument('-j', 'jsdir'  , type=pile.js_dir, help="Set the Javascript directory to download files to")

    parsed = parser.parse_args()

    if parsed.new:
        pile.new()
        
    if parsed.update:
        pile.update()
