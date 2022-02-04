// ConsoleApplication1.cpp : This file contains the 'main' function. Program execution begins and ends there.
//

#define FUSE_USE_VERSION 30
#define FSP_FUSE_CAP_READ_ONLY          (1 << 22)

#if defined (_WIN32) && ! defined (__CYGWIN__)
#define USE_WINFSP 1
#endif

#include <fuse.h>
#include "compat.h"

#include <stdio.h>
#include <sys/stat.h>
#include <sys/types.h>
#include <stddef.h>
#include <errno.h>
#include <time.h>
#include <string.h>
#include <stdlib.h>
#include <assert.h>
#include <time.h>
#include <fcntl.h>

#ifndef O_ACCMODE
#define O_ACCMODE	00000003
#endif

static struct options {
	const char *rda;
	const char *filename;
	const char *contents;
	int show_help;
} options;

#define OPTION(t, p)                           \
    { t, offsetof(struct options, p), 1 }
static const struct fuse_opt option_spec[] = {
	OPTION("--rda=%s", rda),
	OPTION("-h", show_help),
	OPTION("--help", show_help),
	FUSE_OPT_END
};

static int hello_getattr(const char* path, struct fuse_stat* stbuf,
	struct fuse_file_info* fi)
{
	(void)fi;
	int res = 0;

	memset(stbuf, 0, sizeof(struct fuse_stat));
	if (strcmp(path, "/") == 0) {
		stbuf->st_mode = S_IFDIR | 0755;
		stbuf->st_nlink = 2;
	}
	else if (strcmp(path + 1, options.filename) == 0) {
		stbuf->st_mode = S_IFREG | 0444;
		stbuf->st_nlink = 1;
		stbuf->st_size = strlen(options.contents);
	}
	else
		res = -ENOENT;

	return res;
}

static int hello_readdir(const char* path, void* buf, fuse_fill_dir_t filler,
	fuse_off_t offset, struct fuse_file_info* fi,
	enum fuse_readdir_flags flags)
{
	(void)offset;
	(void)fi;
	(void)flags;

	if (strcmp(path, "/") != 0)
		return -ENOENT;

	filler(buf, ".", NULL, 0, FUSE_FILL_DIR_PLUS);
	filler(buf, "..", NULL, 0, FUSE_FILL_DIR_PLUS);
	filler(buf, options.filename, NULL, 0, FUSE_FILL_DIR_PLUS);

	return 0;
}

static int hello_open(const char* path, struct fuse_file_info* fi)
{
	if (strcmp(path + 1, options.filename) != 0)
		return -ENOENT;

	if ((fi->flags & O_ACCMODE) != O_RDONLY)
		return -EACCES;

	return 0;
}

static int hello_read(const char* path, char* buf, size_t size, fuse_off_t offset,
	struct fuse_file_info* fi)
{
	size_t len;
	(void)fi;
	if (strcmp(path + 1, options.filename) != 0)
		return -ENOENT;

	len = strlen(options.contents);
	if (offset < len) {
		if (offset + size > len)
			size = len - offset;
		memcpy(buf, options.contents + offset, size);
	}
	else
		size = 0;

	return size;
}

static void* hello_init(struct fuse_conn_info* conn,
	struct fuse_config* cfg)
{
	conn->want |= (conn->capable & FSP_FUSE_CAP_READ_ONLY);
	cfg->kernel_cache = 1;
	return NULL;
}

static struct fuse_operations operations = { 0 };
static struct fuse_args f_args = { 0 };

int pre_main(int argc, char* argv[]) {
	#ifdef USE_WINFSP
	if (!NT_SUCCESS(FspLoad(0)))
		return ERROR_DELAY_LOAD_FAILED;
    #endif

	struct fuse_args args = FUSE_ARGS_INIT(argc, argv);
	f_args = args;

	/* Parse options */
	if (fuse_opt_parse(&f_args, &options, option_spec, NULL) == -1)
		return 1;
	

	printf("offset st_nlink: %zu\n", offsetof(struct fuse_stat, st_nlink));
	printf("offset st_mode: %zu\n", offsetof(struct fuse_stat, st_mode));
	printf("offset st_uid: %zu\n", offsetof(struct fuse_stat, st_uid));
	printf("offset st_gid: %zu\n", offsetof(struct fuse_stat, st_gid));
	printf("offset st_rdev: %zu\n", offsetof(struct fuse_stat, st_rdev));
	printf("offset st_atim: %zu\n", offsetof(struct fuse_stat, st_atim));
	printf("offset st_mtim: %zu\n", offsetof(struct fuse_stat, st_mtim));
	return 0;
}

void PrintHelpIfNeeded() {
	if (options.show_help) {
		printf("Dummy help MntRDA!");
		assert(fuse_opt_add_arg(&f_args, "--help") == 0);
		f_args.argv[0][0] = '\0';
	}
}

int main()
{
    
	operations.init = hello_init;

	printf("operations.getattr %p,\n", operations.getattr);
	printf("operations.readdir %p,\n", operations.readdir);
	printf("operations.open %p,\n", operations.read);
	printf("operations.read %p,\n", operations.open);

	printf("sizeof: fuse_stat %lld\n", sizeof(struct fuse_stat));
	printf("sizeof fuse_file_info: %lld\n", sizeof(struct fuse_file_info));
	printf("sizeof fuse_off_t: %lld\n", sizeof(fuse_off_t));
	printf("offset fh: %lld\n", offsetof(struct fuse_file_info, fh));


	for(int i=0;i<f_args.argc;i++) {
		printf("f_args.argv[%d]=%s\n",i,f_args.argv[i]);
	}
	int ret = fuse_main(f_args.argc, f_args.argv, &operations, NULL);
	fuse_opt_free_args(&f_args);
	return ret;
}

 void PatchOpen(int (*f)(const char* path, struct fuse_file_info* fi)) {
	printf(".open: %p\n", operations.open);
	operations.open = f;
	printf(".open %p\n", operations.open);
}

void PatchRelease(int (*f)(const char* path, struct fuse_file_info* fi)) {
	printf(".release: %p\n", operations.release);
	operations.release = f;
	printf(".release %p\n", operations.release);
}

 void PatchFuseRead(int (*f)(const char* path, char* buf, size_t size, fuse_off_t off, struct fuse_file_info* fi)) {
	printf(".read: %p\n", operations.read);
	operations.read = f;
	printf(".read: %p\n", operations.read);
}

 void PatchFuseReaddir(int (*f)(const char* path, void* buf, fuse_fill_dir_t filler, fuse_off_t off,
	struct fuse_file_info* fi, enum fuse_readdir_flags)) {
	printf(".readdir: %p\n", operations.readdir);
	operations.readdir = f;
	printf(".readdir: %p\n", operations.readdir);
}

 void PatchFuseGetattr(int (*f)(const char* path, struct fuse_stat* stbuf,
	struct fuse_file_info* fi)) {
	printf(".getattr: %p\n", operations.getattr);
	operations.getattr = f;
	printf(".getattr: %p\n", operations.getattr);
}

const char* GetRdaParameter() {
	return options.rda;
}