require('dotenv').config();
const express = require('express');
const { MongoClient, ObjectId } = require('mongodb');
const cors = require('cors');
const helmet = require('helmet');
const compression = require('compression');
const rateLimit = require('express-rate-limit');
const Joi = require('joi');

const app = express();
const PORT = process.env.PORT || 3000;

// MongoDB Atlas connection
const MONGODB_URI = process.env.MONGODB_URI;
const DB_NAME = process.env.DB_NAME || 'GolfSimulation';

if (!MONGODB_URI) {
    console.error('❌ MONGODB_URI environment variable is required');
    process.exit(1);
}

let db;
let client;

// Security middleware
app.use(helmet({
    contentSecurityPolicy: {
        directives: {
            defaultSrc: ["'self'"],
            styleSrc: ["'self'", "'unsafe-inline'"],
            scriptSrc: ["'self'"],
            imgSrc: ["'self'", "data:", "https:"],
        },
    },
}));

app.use(compression());

// CORS configuration for Unity clients
app.use(cors({
    origin: function (origin, callback) {
        const allowedOrigins = process.env.ALLOWED_ORIGINS?.split(',') || ['*'];
        if (!origin || allowedOrigins.includes('*') || allowedOrigins.includes(origin)) {
            callback(null, true);
        } else {
            callback(new Error('Not allowed by CORS'));
        }
    },
    credentials: true,
    methods: ['GET', 'POST', 'PUT', 'DELETE', 'OPTIONS'],
    allowedHeaders: ['Content-Type', 'Authorization', 'X-API-Key', 'X-Unity-Platform']
}));

app.use(express.json({ limit: '10mb' }));
app.use(express.urlencoded({ extended: true }));

// Rate limiting
const limiter = rateLimit({
    windowMs: parseInt(process.env.RATE_LIMIT_WINDOW_MS) || 15 * 60 * 1000,
    max: parseInt(process.env.RATE_LIMIT_MAX_REQUESTS) || 1000,
    message: {
        error: 'Too many requests from this IP, please try again later.',
        retryAfter: Math.ceil((parseInt(process.env.RATE_LIMIT_WINDOW_MS) || 900000) / 60000)
    },
    standardHeaders: true,
    legacyHeaders: false,
});

app.use('/api/', limiter);

// Request logging middleware
app.use((req, res, next) => {
    console.log(`${new Date().toISOString()} - ${req.method} ${req.path} - ${req.ip}`);
    next();
});

// Connect to MongoDB Atlas
async function connectToMongoDB() {
    try {
        console.log('Connecting to MongoDB Atlas...');
        
        client = new MongoClient(MONGODB_URI, {
            maxPoolSize: 50,           // Reduced for Atlas free tier
            minPoolSize: 2,            // Minimum connections
            maxIdleTimeMS: 30000,      // Close connections after 30s of inactivity
            serverSelectionTimeoutMS: 10000, // Increased timeout for Atlas
            socketTimeoutMS: 45000,    // Socket timeout
            retryWrites: true,         // Enable retryable writes
            w: 'majority'              // Write concern
            // Removed: compression: ['zstd', 'zlib', 'snappy'] - not supported in this driver version
        });
        
        await client.connect();
        
        // Test the connection
        await client.db('admin').command({ ping: 1 });
        
        db = client.db(DB_NAME);
        
        console.log('✅ Connected to MongoDB Atlas successfully');
        console.log(`📊 Database: ${DB_NAME}`);
        console.log(`🌐 Cluster: GolfServer`);
        
        // Create indexes for optimal performance
        await createIndexes();
        
        return true;
        
    } catch (error) {
        console.error('❌ MongoDB Atlas connection failed:', error.message);
        
        // Specific error handling for common Atlas issues
        if (error.message.includes('authentication failed')) {
            console.error('🔐 Authentication failed - check your username and password');
        } else if (error.message.includes('network')) {
            console.error('🌐 Network issue - check your internet connection and IP whitelist');
        } else if (error.message.includes('timeout')) {
            console.error('⏱️  Connection timeout - MongoDB Atlas might be slow to respond');
        } else if (error.message.includes('compression')) {
            console.error('📦 Compression option not supported - using updated connection settings');
        }
        
        return false;
    }
}

// Create optimized indexes for Atlas
async function createIndexes() {
    try {
        console.log('Creating database indexes...');
        
        const indexOperations = [
            // Players collection indexes
            {
                collection: 'players',
                indexes: [
                    { key: { playerId: 1 }, options: { unique: true } },
                    { key: { email: 1 }, options: { unique: true, sparse: true } },
                    { key: { lastSession: -1 } },
                    { key: { 'overallStats.overallSkillLevel': -1 } },
                    { key: { handicap: 1 } },
                    { key: { createdDate: -1 } }
                ]
            },
            
            // Shots collection indexes
            {
                collection: 'shots',
                indexes: [
                    { key: { playerId: 1, timestamp: -1 } },
                    { key: { playerId: 1, clubType: 1, timestamp: -1 } },
                    { key: { sessionId: 1 } },
                    { key: { timestamp: -1 } },
                    { key: { tags: 1 } },
                    { key: { 'ballData.speed': 1 } },
                    { key: { 'contactData.centeredness': -1 } },
                    { key: { clubType: 1 } }
                ]
            },
            
            // Sessions collection indexes
            {
                collection: 'sessions',
                indexes: [
                    { key: { playerId: 1, startTime: -1 } },
                    { key: { sessionId: 1 }, options: { unique: true } },
                    { key: { startTime: -1 } }
                ]
            }
        ];
        
        for (const { collection, indexes } of indexOperations) {
            const coll = db.collection(collection);
            
            for (const { key, options = {} } of indexes) {
                try {
                    await coll.createIndex(key, options);
                    console.log(`✅ Created index on ${collection}:`, key);
                } catch (error) {
                    if (error.code === 85) { // Index already exists
                        console.log(`ℹ️  Index already exists on ${collection}:`, key);
                    } else {
                        console.warn(`⚠️  Index creation warning for ${collection}:`, error.message);
                    }
                }
            }
        }
        
        console.log('Index creation completed');
        
    } catch (error) {
        console.error('Error creating indexes:', error.message);
    }
}

// ==================== VALIDATION SCHEMAS ====================

const shotDataSchema = Joi.object({
    playerId: Joi.string().required(),
    sessionId: Joi.string().optional(),
    clubType: Joi.string().required(),
    ballData: Joi.object({
        speed: Joi.number().min(10).max(90).required(),
        launchAngle: Joi.number().min(-10).max(60).required(),
        launchDirection: Joi.number().min(-90).max(90).required(),
        totalSpin: Joi.number().min(0).max(10000).required(),
        spinAxis: Joi.number().min(-180).max(180).required()
    }).required(),
    clubData: Joi.object({
        clubheadSpeed: Joi.number().min(10).max(150).required(),
        smashFactor: Joi.number().min(0.5).max(2.0).required(),
        attackAngle: Joi.number().min(-20).max(20).required(),
        clubPath: Joi.number().min(-30).max(30).required(),
        faceAngle: Joi.number().min(-30).max(30).required(),
        dynamicLoft: Joi.number().min(0).max(70).optional()
    }).required(),
    contactData: Joi.object({
        centeredness: Joi.number().min(0).max(1).required(),
        impactHeight: Joi.number().optional(),
        impactToe: Joi.number().optional()
    }).required(),
    shotContext: Joi.object({
        shotType: Joi.string().optional(),
        lieCondition: Joi.string().optional(),
        targetDistance: Joi.number().min(0).optional(),
        shotOutcome: Joi.string().optional()
    }).optional(),
    tags: Joi.array().items(Joi.string()).optional(),
    notes: Joi.string().max(500).optional()
});

// ==================== API ENDPOINTS ====================

// Health check endpoint
app.get('/api/health', async (req, res) => {
    try {
        // Test database connection
        const dbStatus = await client.db('admin').command({ ping: 1 });
        
        res.json({ 
            status: 'healthy',
            timestamp: new Date().toISOString(),
            uptime: process.uptime(),
            database: dbStatus.ok === 1 ? 'connected' : 'disconnected',
            environment: process.env.NODE_ENV,
            version: '1.0.0'
        });
    } catch (error) {
        res.status(503).json({
            status: 'unhealthy',
            error: 'Database connection failed',
            timestamp: new Date().toISOString()
        });
    }
});

// Add single shot data
app.post('/api/shots', async (req, res) => {
    try {
        // Validate input data
        const { error, value } = shotDataSchema.validate(req.body);
        if (error) {
            return res.status(400).json({ 
                error: 'Validation failed',
                details: error.details.map(d => d.message)
            });
        }
        
        const shotData = {
            ...value,
            timestamp: new Date(),
            _id: new ObjectId(),
            source: 'unity-client',
            ipAddress: req.ip,
            userAgent: req.get('User-Agent')
        };
        
        const result = await db.collection('shots').insertOne(shotData);
        
        // Trigger async profile update (don't wait for it)
        updatePlayerProfileAsync(shotData.playerId).catch(console.error);
        
        res.status(201).json({ 
            success: true, 
            shotId: result.insertedId,
            message: 'Shot data added successfully'
        });
        
        console.log(`📊 Added shot for player ${shotData.playerId} with ${shotData.clubType}`);
        
    } catch (error) {
        console.error('Error adding shot:', error);
        res.status(500).json({ 
            error: 'Failed to add shot data',
            message: process.env.NODE_ENV === 'development' ? error.message : 'Internal server error'
        });
    }
});

// Batch import shots
app.post('/api/shots/batch', async (req, res) => {
    try {
        const { shots } = req.body;
        
        if (!Array.isArray(shots) || shots.length === 0) {
            return res.status(400).json({ error: 'Invalid shots array' });
        }
        
        if (shots.length > 1000) {
            return res.status(400).json({ error: 'Batch size too large (max 1000 shots)' });
        }
        
        // Validate each shot
        const validatedShots = [];
        const errors = [];
        
        for (let i = 0; i < shots.length; i++) {
            const { error, value } = shotDataSchema.validate(shots[i]);
            if (error) {
                errors.push({ index: i, error: error.details[0].message });
            } else {
                validatedShots.push({
                    ...value,
                    timestamp: new Date(value.timestamp || Date.now()),
                    _id: new ObjectId(),
                    source: 'batch-import'
                });
            }
        }
        
        if (errors.length > 0) {
            return res.status(400).json({
                error: 'Validation failed for some shots',
                errors: errors.slice(0, 10) // Limit error details
            });
        }
        
        const result = await db.collection('shots').insertMany(validatedShots, {
            ordered: false // Continue on individual failures
        });
        
        // Update profiles for all players
        const playerIds = [...new Set(validatedShots.map(s => s.playerId))];
        playerIds.forEach(playerId => {
            updatePlayerProfileAsync(playerId).catch(console.error);
        });
        
        res.json({ 
            success: true,
            imported: result.insertedCount,
            total: shots.length,
            failed: shots.length - result.insertedCount,
            message: `Successfully imported ${result.insertedCount}/${shots.length} shots`
        });
        
        console.log(`📈 Batch imported ${result.insertedCount} shots for ${playerIds.length} players`);
        
    } catch (error) {
        console.error('Error in batch import:', error);
        res.status(500).json({ error: 'Batch import failed' });
    }
});

// Get shot data with advanced filtering
app.get('/api/shots', async (req, res) => {
    try {
        const {
            playerId,
            clubType,
            startDate,
            endDate,
            tags,
            performance,
            limit = 100,
            skip = 0
        } = req.query;
        
        // Build query filter
        const filter = {};
        
        if (playerId) filter.playerId = playerId;
        if (clubType) filter.clubType = clubType;
        
        if (startDate || endDate) {
            filter.timestamp = {};
            if (startDate) filter.timestamp.$gte = new Date(startDate);
            if (endDate) filter.timestamp.$lte = new Date(endDate);
        }
        
        if (tags) {
            const tagArray = tags.split(',').map(tag => tag.trim());
            filter.tags = { $in: tagArray };
        }
        
        // Performance-based filtering
        if (performance === 'best') {
            filter['contactData.centeredness'] = { $gte: 0.8 };
        } else if (performance === 'worst') {
            filter['contactData.centeredness'] = { $lt: 0.5 };
        } else if (performance === 'recent') {
            const thirtyDaysAgo = new Date(Date.now() - 30 * 24 * 60 * 60 * 1000);
            filter.timestamp = { $gte: thirtyDaysAgo };
        }
        
        const limitNum = Math.min(parseInt(limit), 1000); // Max 1000 results
        const skipNum = Math.max(0, parseInt(skip));
        
        const shots = await db.collection('shots')
            .find(filter)
            .sort({ timestamp: -1 })
            .limit(limitNum)
            .skip(skipNum)
            .toArray();
        
        // Get total count for pagination
        const total = await db.collection('shots').countDocuments(filter);
        
        res.json({
            shots,
            pagination: {
                total,
                limit: limitNum,
                skip: skipNum,
                hasMore: (skipNum + limitNum) < total
            }
        });
        
    } catch (error) {
        console.error('Error getting shots:', error);
        res.status(500).json({ error: 'Failed to retrieve shot data' });
    }
});

// Get player profile
app.get('/api/players/:playerId', async (req, res) => {
    try {
        const { playerId } = req.params;
        
        if (!playerId || playerId.length < 3) {
            return res.status(400).json({ error: 'Invalid player ID' });
        }
        
        const player = await db.collection('players').findOne({ playerId });
        
        if (!player) {
            return res.status(404).json({ error: 'Player not found' });
        }
        
        // Remove sensitive fields
        delete player._id;
        
        res.json(player);
        
    } catch (error) {
        console.error('Error getting player:', error);
        res.status(500).json({ error: 'Failed to retrieve player profile' });
    }
});

// Generate realistic shot from player profile
app.post('/api/players/:playerId/generate-shot', async (req, res) => {
    try {
        const { playerId } = req.params;
        const { clubType } = req.body;
        
        if (!clubType) {
            return res.status(400).json({ error: 'Club type is required' });
        }
        
        const player = await db.collection('players').findOne({ playerId });
        if (!player || !player.clubProfiles || !player.clubProfiles[clubType]) {
            return res.status(404).json({ error: 'Player or club profile not found' });
        }
        
        const clubProfile = player.clubProfiles[clubType];
        
        // Check if club has sufficient data
        if (!clubProfile.ballSpeedStats || clubProfile.shotCount < 10) {
            return res.status(400).json({ error: 'Insufficient data for this club' });
        }
        
        const generatedShot = generateStatisticalShot(clubProfile);
        
        res.json(generatedShot);
        
    } catch (error) {
        console.error('Error generating shot:', error);
        res.status(500).json({ error: 'Failed to generate shot' });
    }
});

// ==================== UTILITY FUNCTIONS ====================

// Async function to update player profile
async function updatePlayerProfileAsync(playerId) {
    try {
        // Get recent shots for this player (last 500 shots)
        const recentShots = await db.collection('shots')
            .find({ playerId })
            .sort({ timestamp: -1 })
            .limit(500)
            .toArray();
        
        if (recentShots.length === 0) {
            console.log(`⚠️  No shots found for player ${playerId}`);
            return;
        }
        
        // Calculate updated statistics
        const updatedProfile = calculatePlayerStatistics(playerId, recentShots);
        
        // Update in database with upsert
        const result = await db.collection('players').updateOne(
            { playerId },
            { 
                $set: updatedProfile,
                $setOnInsert: { createdDate: new Date() }
            },
            { upsert: true }
        );
        
        console.log(`📊 Updated profile for player: ${playerId} (${result.modifiedCount ? 'modified' : 'created'})`);
        
    } catch (error) {
        console.error(`❌ Error updating profile for ${playerId}:`, error.message);
    }
}

function calculatePlayerStatistics(playerId, shots) {
    // Group shots by club type
    const clubGroups = shots.reduce((groups, shot) => {
        const club = shot.clubType;
        if (!groups[club]) groups[club] = [];
        groups[club].push(shot);
        return groups;
    }, {});
    
    // Calculate statistics for each club
    const clubProfiles = {};
    for (const [clubType, clubShots] of Object.entries(clubGroups)) {
        if (clubShots.length >= 5) { // Minimum shots for meaningful stats
            clubProfiles[clubType] = calculateClubStatistics(clubShots);
        }
    }
    
    // Calculate overall statistics
    const overallStats = calculateOverallStats(shots);
    const playingCharacteristics = calculatePlayingCharacteristics(shots);
    
    return {
        playerId,
        playerName: playerId, // Default to playerId, can be updated later
        lastUpdated: new Date(),
        totalShots: shots.length,
        lastSession: shots[0].timestamp, // Most recent shot
        overallStats,
        playingCharacteristics,
        clubProfiles
    };
}

function calculateClubStatistics(clubShots) {
    const ballSpeeds = clubShots.map(s => s.ballData?.speed || 0).filter(s => s > 0);
    const launchAngles = clubShots.map(s => s.ballData?.launchAngle || 0);
    const totalSpins = clubShots.map(s => s.ballData?.totalSpin || 0);
    const centeredness = clubShots.map(s => s.contactData?.centeredness || 0);
    const smashFactors = clubShots.map(s => s.clubData?.smashFactor || 0).filter(s => s > 0);
    
    return {
        clubName: clubShots[0].clubType,
        clubCategory: getClubCategory(clubShots[0].clubType),
        shotCount: clubShots.length,
        ballSpeedStats: calculateStatistics(ballSpeeds),
        launchAngleStats: calculateStatistics(launchAngles),
        spinStats: calculateStatistics(totalSpins),
        accuracyStats: {
            centerednessAverage: average(centeredness),
            shotDispersion: calculateDispersion(clubShots),
            smashFactorConsistency: 1 / (1 + standardDeviation(smashFactors))
        },
        performanceMetrics: {
            skillLevel: calculateSkillLevel(clubShots),
            contactQuality: average(centeredness),
            recentTrend: 0, // Would need historical comparison
            hasRecentData: clubShots.some(s => (Date.now() - new Date(s.timestamp)) < 30 * 24 * 60 * 60 * 1000)
        },
        lastUpdated: new Date()
    };
}

function calculateStatistics(values) {
    if (values.length === 0) return { mean: 0, standardDeviation: 0, min: 0, max: 0 };
    
    const sorted = [...values].sort((a, b) => a - b);
    const mean = average(values);
    const variance = average(values.map(v => Math.pow(v - mean, 2)));
    
    return {
        mean,
        standardDeviation: Math.sqrt(variance),
        min: Math.min(...values),
        max: Math.max(...values),
        median: sorted[Math.floor(sorted.length / 2)],
        percentile25: sorted[Math.floor(sorted.length * 0.25)],
        percentile75: sorted[Math.floor(sorted.length * 0.75)]
    };
}

function calculateOverallStats(shots) {
    const centeredness = shots.map(s => s.contactData?.centeredness || 0);
    const smashFactors = shots.map(s => s.clubData?.smashFactor || 0).filter(s => s > 0);
    
    const avgCenteredness = average(centeredness);
    const avgSmashFactor = average(smashFactors);
    
    return {
        overallSkillLevel: Math.min(1, avgCenteredness * (avgSmashFactor / 1.4)), // Normalize smash factor
        consistency: 1 / (1 + standardDeviation(centeredness) * 2),
        improvement: 0 // Would need historical comparison
    };
}

function calculatePlayingCharacteristics(shots) {
    const attackAngles = shots.map(s => s.clubData?.attackAngle || 0);
    const clubPaths = shots.map(s => s.clubData?.clubPath || 0);
    
    return {
        aggressiveness: Math.max(0, Math.min(1, (average(attackAngles) + 5) / 10)),
        courseManagement: 0.5, // Default - would need course-specific data
        shortGameSkill: calculateShortGameSkill(shots),
        drivingAccuracy: calculateDrivingAccuracy(shots),
        weatherAdaptability: 0.5 // Default
    };
}

function calculateShortGameSkill(shots) {
    const shortGameShots = shots.filter(s => {
        const club = s.clubType.toLowerCase();
        return club.includes('wedge') || club.includes('sand') || club.includes('lob');
    });
    
    if (shortGameShots.length === 0) return 0.5;
    
    const centeredness = shortGameShots.map(s => s.contactData?.centeredness || 0);
    return average(centeredness);
}

function calculateDrivingAccuracy(shots) {
    const driverShots = shots.filter(s => s.clubType.toLowerCase().includes('driver'));
    
    if (driverShots.length === 0) return 0.5;
    
    const directions = driverShots.map(s => s.ballData?.launchDirection || 0);
    const directionStdDev = standardDeviation(directions);
    
    return Math.max(0, Math.min(1, 1 / (1 + directionStdDev / 5)));
}

function generateStatisticalShot(clubProfile) {
    // Generate shot using normal distribution (Box-Muller transform)
    function sampleNormal(mean, stdDev) {
        const u1 = Math.random();
        const u2 = Math.random();
        const z = Math.sqrt(-2 * Math.log(u1)) * Math.cos(2 * Math.PI * u2);
        return mean + stdDev * z;
    }
    
    const ballSpeedStats = clubProfile.ballSpeedStats || { mean: 45, standardDeviation: 3 };
    const launchAngleStats = clubProfile.launchAngleStats || { mean: 15, standardDeviation: 2 };
    const spinStats = clubProfile.spinStats || { mean: 3000, standardDeviation: 500 };
    
    return {
        velocity: Math.max(10, sampleNormal(ballSpeedStats.mean, ballSpeedStats.standardDeviation)),
        angle: Math.max(0, Math.min(50, sampleNormal(launchAngleStats.mean, launchAngleStats.standardDeviation))),
        rpm: Math.max(0, sampleNormal(spinStats.mean, spinStats.standardDeviation * 0.8)), // Backspin
        sidespin: sampleNormal(0, 200), // Random sidespin
        temperature_K: 288.15
    };
}

// Helper math functions
function average(arr) {
    return arr.length ? arr.reduce((a, b) => a + b, 0) / arr.length : 0;
}

function standardDeviation(arr) {
    if (arr.length === 0) return 0;
    const avg = average(arr);
    const variance = average(arr.map(x => Math.pow(x - avg, 2)));
    return Math.sqrt(variance);
}

function calculateDispersion(shots) {
    const directions = shots.map(s => s.ballData?.launchDirection || 0);
    return standardDeviation(directions) * 3; // Convert to approximate meters at 150m
}

function calculateSkillLevel(shots) {
    const centeredness = shots.map(s => s.contactData?.centeredness || 0);
    const smashFactors = shots.map(s => s.clubData?.smashFactor || 0).filter(s => s > 0);
    
    const contactQuality = average(centeredness);
    const efficiency = average(smashFactors) / 1.4; // Normalize to 0-1 scale
    const consistency = 1 / (1 + standardDeviation(centeredness) * 2);
    
    return Math.min(1, (contactQuality + efficiency + consistency) / 3);
}

function getClubCategory(clubType) {
    const club = clubType.toLowerCase();
    if (club.includes('driver')) return 'driver';
    if (club.includes('wood') || club.includes('hybrid')) return 'fairway';
    if (club.includes('iron')) return 'iron';
    if (club.includes('wedge') || club.includes('sand') || club.includes('lob')) return 'wedge';
    if (club.includes('putter')) return 'putter';
    return 'iron'; // default
}

// Error handling middleware
app.use((error, req, res, next) => {
    console.error('❌ Unhandled error:', error);
    res.status(500).json({ 
        error: 'Internal server error',
        timestamp: new Date().toISOString()
    });
});

// 404 handler
app.use('*', (req, res) => {
    res.status(404).json({ 
        error: 'Endpoint not found',
        path: req.originalUrl,
        method: req.method,
        timestamp: new Date().toISOString()
    });
});

// ==================== SERVER STARTUP ====================

async function startServer() {
    try {
        console.log('🚀 Starting Golf Simulation API Server...');
        console.log(`📦 Node.js version: ${process.version}`);
        console.log(`🌍 Environment: ${process.env.NODE_ENV || 'development'}`);
        
        const connected = await connectToMongoDB();
        if (!connected) {
            console.error('❌ Failed to connect to database. Exiting...');
            process.exit(1);
        }
        
        app.listen(PORT, () => {
            console.log(`✅ Golf API server running on port ${PORT}`);
            console.log(`📡 Health check: http://localhost:${PORT}/api/health`);
            console.log(`📊 API Base URL: http://localhost:${PORT}/api`);
            console.log(`🔗 MongoDB Atlas Connected: ${DB_NAME}`);
            console.log('');
            console.log('Available endpoints:');
            console.log('  POST /api/shots - Add shot data');
            console.log('  POST /api/shots/batch - Batch import shots');
            console.log('  GET  /api/shots - Get shot data with filters');
            console.log('  GET  /api/players/:playerId - Get player profile');
            console.log('  POST /api/players/:playerId/generate-shot - Generate realistic shot');
            console.log('  GET  /api/health - Health check');
            console.log('');
        });
        
    } catch (error) {
        console.error('❌ Failed to start server:', error);
        process.exit(1);
    }
}

// Graceful shutdown handling
process.on('SIGINT', async () => {
    console.log('\n🛑 Shutting down gracefully...');
    try {
        if (client) {
            await client.close();
            console.log('📊 MongoDB Atlas connection closed');
        }
        console.log('✅ Server shutdown complete');
        process.exit(0);
    } catch (error) {
        console.error('❌ Error during shutdown:', error);
        process.exit(1);
    }
});

process.on('SIGTERM', async () => {
    console.log('🛑 SIGTERM received, shutting down...');
    if (client) {
        await client.close();
    }
    process.exit(0);
});

// Handle uncaught exceptions
process.on('uncaughtException', (error) => {
    console.error('💥 Uncaught Exception:', error);
    process.exit(1);
});

process.on('unhandledRejection', (reason, promise) => {
    console.error('💥 Unhandled Rejection at:', promise, 'reason:', reason);
    process.exit(1);
});

// Start the server
startServer();

module.exports = { app, db, client }; // Export for testing
