const { MongoClient } = require('mongodb');
require('dotenv').config();

async function setupDatabase() {
    console.log('🔧 Setting up Golf Simulation Database...');
    
    const client = new MongoClient(process.env.MONGODB_URI, {
        maxPoolSize: 50,           // Maximum connections in pool
        minPoolSize: 2,            // Minimum connections
        serverSelectionTimeoutMS: 10000, // Server selection timeout
        socketTimeoutMS: 45000,    // Socket timeout
        retryWrites: true,         // Enable retryable writes
        w: 'majority'              // Write concern
    });
    
    try {
        console.log('🔌 Connecting to MongoDB Atlas...');
        await client.connect();
        
        // Test the connection
        await client.db('admin').command({ ping: 1 });
        console.log('✅ Connected to MongoDB Atlas successfully');
        
        const db = client.db(process.env.DB_NAME || 'GolfSimulation');
        console.log(`📊 Using database: ${process.env.DB_NAME || 'GolfSimulation'}`);
        
        // Create collections with validation rules
        console.log('📁 Creating collections...');
        
        // Players collection with schema validation
        try {
            await db.createCollection('players', {
                validator: {
                    $jsonSchema: {
                        bsonType: 'object',
                        required: ['playerId', 'lastUpdated'],
                        properties: {
                            playerId: {
                                bsonType: 'string',
                                description: 'Player ID must be a string and is required'
                            },
                            playerName: {
                                bsonType: 'string',
                                description: 'Player name must be a string'
                            },
                            email: {
                                bsonType: 'string',
                                pattern: '^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$',
                                description: 'Email must be a valid email address'
                            },
                            handicap: {
                                bsonType: 'number',
                                minimum: -10,
                                maximum: 54,
                                description: 'Handicap must be between -10 and 54'
                            },
                            lastUpdated: {
                                bsonType: 'date',
                                description: 'Last updated must be a date'
                            }
                        }
                    }
                }
            });
            console.log('✅ Created players collection with validation');
        } catch (error) {
            if (error.code === 48) {
                console.log('ℹ️  Players collection already exists');
            } else {
                console.warn('⚠️  Players collection creation warning:', error.message);
            }
        }
        
        // Shots collection with schema validation
        try {
            await db.createCollection('shots', {
                validator: {
                    $jsonSchema: {
                        bsonType: 'object',
                        required: ['playerId', 'clubType', 'ballData', 'timestamp'],
                        properties: {
                            playerId: {
                                bsonType: 'string',
                                description: 'Player ID must be a string and is required'
                            },
                            clubType: {
                                bsonType: 'string',
                                description: 'Club type must be a string and is required'
                            },
                            ballData: {
                                bsonType: 'object',
                                required: ['speed', 'launchAngle', 'totalSpin'],
                                properties: {
                                    speed: {
                                        bsonType: 'number',
                                        minimum: 10,
                                        maximum: 90,
                                        description: 'Ball speed must be between 10-90 m/s'
                                    },
                                    launchAngle: {
                                        bsonType: 'number',
                                        minimum: -10,
                                        maximum: 60,
                                        description: 'Launch angle must be between -10 and 60 degrees'
                                    },
                                    totalSpin: {
                                        bsonType: 'number',
                                        minimum: 0,
                                        maximum: 10000,
                                        description: 'Total spin must be between 0-10000 RPM'
                                    }
                                }
                            },
                            timestamp: {
                                bsonType: 'date',
                                description: 'Timestamp must be a date'
                            }
                        }
                    }
                }
            });
            console.log('✅ Created shots collection with validation');
        } catch (error) {
            if (error.code === 48) {
                console.log('ℹ️  Shots collection already exists');
            } else {
                console.warn('⚠️  Shots collection creation warning:', error.message);
            }
        }
        
        // Sessions collection (simple, no validation)
        try {
            await db.createCollection('sessions');
            console.log('✅ Created sessions collection');
        } catch (error) {
            if (error.code === 48) {
                console.log('ℹ️  Sessions collection already exists');
            } else {
                console.warn('⚠️  Sessions collection creation warning:', error.message);
            }
        }
        
        // Create indexes
        console.log('📝 Creating indexes...');
        
        const indexOperations = [
            // Players collection
            { collection: 'players', index: { playerId: 1 }, options: { unique: true } },
            { collection: 'players', index: { email: 1 }, options: { unique: true, sparse: true } },
            { collection: 'players', index: { lastSession: -1 } },
            
            // Shots collection  
            { collection: 'shots', index: { playerId: 1, timestamp: -1 } },
            { collection: 'shots', index: { playerId: 1, clubType: 1, timestamp: -1 } },
            { collection: 'shots', index: { clubType: 1 } },
            { collection: 'shots', index: { timestamp: -1 } },
            { collection: 'shots', index: { 'ballData.speed': 1 } },
            { collection: 'shots', index: { 'contactData.centeredness': -1 } },
            
            // Sessions collection
            { collection: 'sessions', index: { sessionId: 1 }, options: { unique: true } },
            { collection: 'sessions', index: { playerId: 1, startTime: -1 } }
        ];
        
        for (const { collection, index, options = {} } of indexOperations) {
            try {
                await db.collection(collection).createIndex(index, options);
                console.log(`✅ Created index on ${collection}:`, Object.keys(index).join(', '));
            } catch (error) {
                if (error.code === 85) {
                    console.log(`ℹ️  Index already exists on ${collection}:`, Object.keys(index).join(', '));
                } else {
                    console.warn(`⚠️  Index creation failed for ${collection}:`, error.message);
                }
            }
        }
        
        // Insert sample data for testing
        console.log('📊 Inserting sample data...');
        
        const samplePlayer = {
            playerId: 'test-player-001',
            playerName: 'Test Player',
            email: 'test@example.com',
            handicap: 15.5,
            skillCategory: 'amateur',
            createdDate: new Date(),
            lastSession: new Date(),
            lastUpdated: new Date(),
            totalShots: 0,
            overallStats: {
                overallSkillLevel: 0.6,
                consistency: 0.7,
                improvement: 0.1
            },
            playingCharacteristics: {
                aggressiveness: 0.5,
                courseManagement: 0.6,
                shortGameSkill: 0.5,
                drivingAccuracy: 0.6,
                weatherAdaptability: 0.5
            },
            clubProfiles: {}
        };
        
        try {
            await db.collection('players').insertOne(samplePlayer);
            console.log('✅ Inserted sample player data');
        } catch (error) {
            if (error.code === 11000) {
                console.log('ℹ️  Sample player already exists');
            } else {
                console.warn('⚠️  Sample player insertion warning:', error.message);
            }
        }
        
        const sampleShot = {
            playerId: 'test-player-001',
            sessionId: 'session-001',
            clubType: '7Iron',
            ballData: {
                speed: 42.5,
                launchAngle: 18.2,
                launchDirection: -1.5,
                totalSpin: 5200,
                spinAxis: 15.0
            },
            clubData: {
                clubheadSpeed: 85.0,
                smashFactor: 1.32,
                attackAngle: -2.1,
                clubPath: 1.2,
                faceAngle: -0.8,
                dynamicLoft: 28.5
            },
            contactData: {
                centeredness: 0.85,
                impactHeight: 2.1,
                impactToe: -1.2
            },
            shotContext: {
                shotType: 'approach',
                lieCondition: 'fairway',
                targetDistance: 140,
                shotOutcome: 'good'
            },
            timestamp: new Date(),
            source: 'sample-data',
            tags: ['practice', 'iron-play']
        };
        
        try {
            await db.collection('shots').insertOne(sampleShot);
            console.log('✅ Inserted sample shot data');
        } catch (error) {
            console.warn('⚠️  Sample shot insertion warning:', error.message);
        }
        
        console.log('');
        console.log('🎉 Database setup completed successfully!');
        console.log('📊 Collections created: players, shots, sessions');
        console.log('📝 Indexes created for optimal performance');
        console.log('🧪 Sample data inserted for testing');
        console.log('');
        console.log('Your MongoDB Atlas cluster is ready for the Golf Simulation!');
        console.log(`🔗 Database: ${process.env.DB_NAME || 'GolfSimulation'}`);
        console.log(`🌐 Cluster: GolfServer`);
        
    } catch (error) {
        console.error('❌ Database setup failed:', error.message);
        
        // Provide specific troubleshooting for common issues
        if (error.message.includes('authentication failed')) {
            console.error('🔐 Check your username and password in the connection string');
        } else if (error.message.includes('network')) {
            console.error('🌐 Check your internet connection and MongoDB Atlas IP whitelist');
        } else if (error.message.includes('timeout')) {
            console.error('⏱️  Connection timeout - try again or check Atlas cluster status');
        }
        
        throw error;
    } finally {
        await client.close();
        console.log('📊 MongoDB connection closed');
    }
}

// Run setup if this file is executed directly
if (require.main === module) {
    setupDatabase()
        .then(() => {
            console.log('✅ Setup completed successfully');
            process.exit(0);
        })
        .catch((error) => {
            console.error('❌ Setup failed:', error.message);
            process.exit(1);
        });
}

module.exports = { setupDatabase };

// Simple connection test script
async function testConnection() {
    console.log('🧪 Testing MongoDB Atlas connection...');
    
    const client = new MongoClient(process.env.MONGODB_URI, {
        serverSelectionTimeoutMS: 5000,
        retryWrites: true
    });
    
    try {
        await client.connect();
        await client.db('admin').command({ ping: 1 });
        console.log('✅ Connection test successful!');
        
        const db = client.db(process.env.DB_NAME || 'GolfSimulation');
        const collections = await db.listCollections().toArray();
        console.log(`📁 Found ${collections.length} collections:`, collections.map(c => c.name));
        
        return true;
    } catch (error) {
        console.error('❌ Connection test failed:', error.message);
        return false;
    } finally {
        await client.close();
    }
}

module.exports = { setupDatabase, testConnection };